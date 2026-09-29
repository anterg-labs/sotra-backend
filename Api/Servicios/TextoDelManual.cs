using System.Text.RegularExpressions;

namespace Api.Servicios;

public static class TextoDelManual
{
    public const string MarcaDeSubtitulo = "¶";

    private const int LargoMaximoDeSeccionUsadaComoContexto = 150;
    private const int LargoMaximoDeResumenExtractivo = 500;
    private const int CaracteresParaDetectarRepeticion = 40;
    private const int LetrasMinimasDeTitulo = 10;

    private static readonly string[] PalabrasDeAviso =
    {
        "ADVERTENCIA", "PRECAUCIÓN", "PRECAUCION", "PELIGRO", "AVISO", "IMPORTANTE", "NOTA", "COMENTARIO"
    };

    private static readonly string[] ConectoresAlFinalDeTitulo =
    {
        "Y", "O", "E", "DE", "DEL", "LA", "EL", "LOS", "LAS", "EN", "A", "AL", "CON", "PARA", "POR"
    };

    private static readonly Regex PatronEspacios = new("\\s+");

    private static readonly Regex PatronEncabezadoDePagina = new(
        "(?:\\b[A-ZÁÉÍÓÚÑÜ]{2,}\\s+){1,6}\\d{1,2}-\\d{1,3}(?=\\s|$)");

    private static readonly Regex PatronNumeroPegadoAlTitulo = new(
        "([A-ZÁÉÍÓÚÑÜ]{3,})\\d(?=\\s+[A-ZÁÉÍÓÚÑÜ(¿])");

    private static readonly Regex PatronNumeroSueltoDespuesDelTitulo = new(
        "(\\b[A-ZÁÉÍÓÚÑÜ]{3,})\\s\\d\\s(?=[A-ZÁÉÍÓÚÑÜ(¿])");

    private static readonly Regex PatronVinetaMalExtraida = new("(?<=^|\\s)l\\s+(?=[A-ZÁÉÍÓÚÑÜ¿])");

    private static readonly Regex PatronSubtituloConNumero = new(
        "(?<=[.:]\\s)([A-ZÁÉÍÓÚÑÜ][a-záéíóúñü]+(?:\\s[a-záéíóúñü]+){0,3})\\s\\d\\s(?=[A-ZÁÉÍÓÚÑÜ¿•])");

    private static readonly Regex PatronSubtituloAntesDeVineta = new(
        "(?<=[.:]\\s)([A-ZÁÉÍÓÚÑÜ][a-záéíóúñü]+)\\s(?=•)");

    private static readonly Regex PatronTitulo = new(
        "(?<![“\"(\\w])[A-ZÁÉÍÓÚÑÜ][A-ZÁÉÍÓÚÑÜ\\-]{2,}(?![a-záéíóúñü])" +
        "(?:,?\\s+(?:[A-ZÁÉÍÓÚÑÜ][A-ZÁÉÍÓÚÑÜ\\-]*|\\d+)(?![a-záéíóúñü.\\d]))+");

    private static readonly Regex PatronInicioDeReferenciaCruzada = new("^(?:\\(\\d|[“”\"])");

    public static string LimpiarRuido(string texto)
    {
        var normalizado = DocumentoPdf.Normalizar(texto);
        var sinEncabezados = PatronEncabezadoDePagina.Replace(normalizado, " ");
        var sinNumerosPegados = PatronNumeroPegadoAlTitulo.Replace(sinEncabezados, "$1");
        var sinNumerosSueltos = PatronNumeroSueltoDespuesDelTitulo.Replace(sinNumerosPegados, "$1 ");
        var conVinetasCorregidas = PatronVinetaMalExtraida.Replace(sinNumerosSueltos, "• ");
        var conSubtitulosNumerados = PatronSubtituloConNumero.Replace(conVinetasCorregidas, MarcaDeSubtitulo + " $1: ");
        var conSubtitulosMarcados = PatronSubtituloAntesDeVineta.Replace(conSubtitulosNumerados, MarcaDeSubtitulo + " $1: ");
        var conEspaciosSimples = PatronEspacios.Replace(conSubtitulosMarcados, " ");
        return conEspaciosSimples.Trim();
    }

    public static List<SeccionDocumento> DividirEnSecciones(FuenteDocumento fuente)
    {
        var texto = LimpiarRuido(fuente.Texto);
        var titulos = BuscarTitulos(texto);
        var secciones = new List<SeccionDocumento>();

        var inicioPrimerTitulo = texto.Length;
        if (titulos.Count > 0)
        {
            inicioPrimerTitulo = titulos[0].Inicio;
        }

        var textoAntesDelPrimerTitulo = texto.Substring(0, inicioPrimerTitulo).Trim();
        if (textoAntesDelPrimerTitulo.Length > 0)
        {
            var noHayTitulos = titulos.Count == 0;
            secciones.Add(new SeccionDocumento(fuente.Id, fuente.Pagina, fuente.Documento,
                "", "", textoAntesDelPrimerTitulo, noHayTitulos));
        }

        var contexto = "";
        for (var indice = 0; indice < titulos.Count; indice++)
        {
            var titulo = titulos[indice];
            var finCuerpo = texto.Length;
            var llegaAlFinal = true;
            if (indice + 1 < titulos.Count)
            {
                finCuerpo = titulos[indice + 1].Inicio;
                llegaAlFinal = false;
            }

            var cuerpo = texto.Substring(titulo.Fin, finCuerpo - titulo.Fin).Trim(' ', ',');
            secciones.Add(new SeccionDocumento(fuente.Id, fuente.Pagina, fuente.Documento,
                titulo.Texto, contexto, cuerpo, llegaAlFinal));

            contexto = "";
            if (cuerpo.Length < LargoMaximoDeSeccionUsadaComoContexto && !llegaAlFinal)
            {
                contexto = CrearTituloLegible(titulo.Texto) + ". " + cuerpo;
            }
        }

        return secciones;
    }

    public static string UnirContinuacion(string cuerpo, string continuacion)
    {
        var finalDelCuerpo = cuerpo;
        if (cuerpo.Length > CaracteresParaDetectarRepeticion)
        {
            finalDelCuerpo = cuerpo.Substring(cuerpo.Length - CaracteresParaDetectarRepeticion);
        }

        var posicionRepetida = continuacion.IndexOf(finalDelCuerpo, StringComparison.Ordinal);
        if (posicionRepetida == -1)
        {
            return cuerpo + " " + continuacion;
        }

        var textoNuevo = continuacion.Substring(posicionRepetida + finalDelCuerpo.Length).Trim();
        return cuerpo + " " + textoNuevo;
    }

    public static string QuitarMarcasInternas(string texto)
    {
        var sinMarcas = texto.Replace(MarcaDeSubtitulo, " ");
        return PatronEspacios.Replace(sinMarcas, " ").Trim();
    }

    public static string CrearResumenExtractivo(string texto)
    {
        var sinMarcas = QuitarMarcasInternas(texto);
        var sinVinetas = PatronEspacios.Replace(sinMarcas.Replace("•", " "), " ").Trim();
        return RecortarEnOracion(sinVinetas, LargoMaximoDeResumenExtractivo);
    }

    public static string CrearTituloLegible(string titulo)
    {
        if (titulo.Length == 0)
        {
            return "";
        }

        var minusculas = titulo.ToLowerInvariant();
        var primeraLetra = char.ToUpperInvariant(minusculas[0]);
        return primeraLetra + minusculas.Substring(1);
    }

    public static string RecortarEnOracion(string texto, int largoMaximo)
    {
        if (texto.Length <= largoMaximo)
        {
            return texto;
        }

        var recorte = texto.Substring(0, largoMaximo);
        var ultimoPunto = recorte.LastIndexOf(". ");
        if (ultimoPunto > 0)
        {
            return recorte.Substring(0, ultimoPunto + 1);
        }

        var ultimoEspacio = recorte.LastIndexOf(' ');
        if (ultimoEspacio > 0)
        {
            recorte = recorte.Substring(0, ultimoEspacio);
        }

        return recorte.Trim() + "…";
    }

    private static List<TituloEncontrado> BuscarTitulos(string texto)
    {
        var titulos = new List<TituloEncontrado>();
        foreach (Match coincidencia in PatronTitulo.Matches(texto))
        {
            var palabrasTitulo = TomarPalabrasAntesDeUnAviso(coincidencia.Value);
            QuitarPalabrasInvalidasDelFinal(palabrasTitulo);

            var titulo = string.Join(" ", palabrasTitulo).TrimEnd(',');
            var tienePocasPalabras = palabrasTitulo.Count < 2;
            var tienePocasLetras = ContarLetras(titulo) < LetrasMinimasDeTitulo;
            if (tienePocasPalabras || tienePocasLetras)
            {
                continue;
            }

            var inicioTitulo = coincidencia.Index;
            var finTitulo = inicioTitulo + titulo.Length;
            if (EsReferenciaCruzada(texto, finTitulo))
            {
                continue;
            }

            titulos.Add(new TituloEncontrado(inicioTitulo, finTitulo, titulo));
        }

        return titulos;
    }

    private static List<string> TomarPalabrasAntesDeUnAviso(string textoEnMayusculas)
    {
        var palabras = new List<string>();
        var todasLasPalabras = textoEnMayusculas.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var palabra in todasLasPalabras)
        {
            var palabraSinComa = palabra.Trim(',');
            if (PalabrasDeAviso.Contains(palabraSinComa))
            {
                break;
            }

            palabras.Add(palabra);
        }

        return palabras;
    }

    private static void QuitarPalabrasInvalidasDelFinal(List<string> palabras)
    {
        while (palabras.Count > 0)
        {
            var ultimaPalabra = palabras[palabras.Count - 1];
            if (!EsFinalInvalidoDeTitulo(ultimaPalabra))
            {
                return;
            }

            palabras.RemoveAt(palabras.Count - 1);
        }
    }

    private static int ContarLetras(string texto)
    {
        var cantidad = 0;
        foreach (var caracter in texto)
        {
            if (char.IsLetter(caracter))
            {
                cantidad++;
            }
        }

        return cantidad;
    }

    private static bool EsReferenciaCruzada(string texto, int finTitulo)
    {
        var textoSiguiente = texto.Substring(finTitulo).TrimStart();
        return PatronInicioDeReferenciaCruzada.IsMatch(textoSiguiente);
    }

    private static bool EsFinalInvalidoDeTitulo(string palabra)
    {
        var palabraLimpia = palabra.Trim(',');
        var esNumeroSuelto = palabraLimpia.Length == 1 && char.IsDigit(palabraLimpia[0]);
        if (esNumeroSuelto)
        {
            return true;
        }

        return ConectoresAlFinalDeTitulo.Contains(palabraLimpia);
    }
}
