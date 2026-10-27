using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Api.Servicios;

public static class DocumentoPdf
{
    private const int LargoMaximoDeFragmento = 1400;
    private const int LargoMinimoDeFragmento = 700;
    private const int CaracteresRepetidosEntreFragmentos = 200;
    private const string GuionInvisible = "\u00ad";
    private const int PalabrasMaximasDeTituloNumerado = 10;

    // Encabezados y pies: una línea del borde de la página que se repite en al menos tantas páginas. Arriba se
    // revisan más líneas porque algunos manuales repiten un índice lateral de una palabra por línea.
    private const int PaginasMinimasParaSerRepetida = 3;
    private const int LineasRevisadasArriba = 16;
    private const int LineasRevisadasAbajo = 4;

    private static readonly Regex PatronCaracteresDeControl = new("[\\x00-\\x08\\x0B\\x0C\\x0E-\\x1F]");
    private static readonly Regex PatronEspacios = new("\\s+");

    // Números de página ("5-74", "4-29", "123"): se igualan para que el encabezado de cada página dé la misma clave.
    private static readonly Regex PatronNumeroDePagina = new("\\d+\\s*[-–]\\s*\\d+|^\\d+$");

    // Etiquetas de figura en una línea propia ("Fig. 5-108"): no son contenido y cortan las secciones.
    private static readonly Regex PatronEtiquetaDeFigura = new(
        "^(?:fig(?:ura|ure)?)\\.?\\s*\\d+(?:\\s*[-–.]\\s*\\d+)*[a-z]?$", RegexOptions.IgnoreCase);

    // Arriba no se quita una palabra de aviso aunque se repita: suele abrir un recuadro de advertencia.
    private static readonly HashSet<string> PalabrasDeAvisoQueSeConservanArriba = new()
    {
        "advertencia", "precaución", "precaucion", "peligro", "aviso", "importante", "nota", "observación",
        "warning", "caution", "danger", "notice", "important", "note", "remark"
    };

    // Una l\u00ednea como "4.2 Cambio del filtro": n\u00famero con punto, may\u00fascula inicial y sin puntuaci\u00f3n de oraci\u00f3n.
    // Los pasos numerados ("1. Retire el filtro.") terminan en punto y no se marcan.
    private static readonly Regex PatronLineaDeTituloNumerado = new(
        "^\\d{1,2}\\.(?:\\d{1,2}\\.?)*\\s+\\p{Lu}[^.:;!?\u00a7]{1,78}$");

    // T\u00edtulo sin n\u00famero de secci\u00f3n: una l\u00ednea corta en may\u00fasculas ("500 HOURS OF SERVICE") o con may\u00fascula inicial
    // ("Primary Fuel Filter"), sin puntuaci\u00f3n de oraci\u00f3n.
    private static readonly Regex PatronLineaDeTituloSinNumero = new(
        "^(?:\\d{1,4}\\s+)?\\p{Lu}[^.:;,!?\u00a7\u2022]{1,78}$");

    private static readonly Regex PatronInicioDeProcedimiento = new("^1[.)]\\s");

    private const int PalabrasMinimasDeTituloSinNumero = 2;
    private const int PalabrasMaximasDeTituloSinNumero = 8;
    private const int LetrasMinimasDeTituloEnMayusculas = 6;

    // Una l\u00ednea que termina en una de estas palabras sigue en la l\u00ednea siguiente: es una oraci\u00f3n cortada.
    private static readonly HashSet<string> ConectoresAlFinalDeLinea = new(StringComparer.OrdinalIgnoreCase)
    {
        "y", "o", "e", "de", "del", "la", "el", "los", "las", "en", "a", "al", "con", "para", "por", "que",
        "and", "or", "of", "the", "to", "in", "on", "for", "with", "an", "from", "by", "at", "into"
    };

    // Una l\u00ednea en may\u00fasculas con una palabra de aviso abre un recuadro de advertencia, no una secci\u00f3n.
    private static readonly HashSet<string> PalabrasDeAviso = new(StringComparer.OrdinalIgnoreCase)
    {
        "ADVERTENCIA", "PRECAUCI\u00d3N", "PRECAUCION", "PELIGRO", "AVISO", "IMPORTANTE", "NOTA",
        "WARNING", "CAUTION", "DANGER", "NOTICE", "IMPORTANT", "NOTE"
    };

    public static List<FragmentoDocumento> Extraer(byte[] archivo)
    {
        var fragmentos = new List<FragmentoDocumento>();
        using var memoria = new MemoryStream(archivo);
        using var documento = PdfDocument.Open(memoria);

        var numerosDePagina = new List<int>();
        var textosDePagina = new List<string>();
        foreach (var pagina in documento.GetPages())
        {
            numerosDePagina.Add(pagina.Number);
            textosDePagina.Add(ContentOrderTextExtractor.GetText(pagina));
        }

        var textosLimpios = QuitarEncabezadosYPies(textosDePagina);
        for (var indice = 0; indice < textosLimpios.Count; indice++)
        {
            var texto = Normalizar(MarcarTitulosNumerados(textosLimpios[indice]));
            foreach (var fragmento in Dividir(texto))
            {
                fragmentos.Add(new FragmentoDocumento(numerosDePagina[indice], fragmento));
            }
        }

        return fragmentos;
    }

    /// <summary>
    /// Quita de cada página el encabezado y el pie que el manual repite (título del capítulo, número de página,
    /// índice lateral, dirección web) y las etiquetas de figura. Si quedaran, la página siguiente no parecería la
    /// continuación de la sección y la sugerencia se cortaría en el cambio de página. Recibe el texto de cada
    /// página con sus saltos de línea, antes de <see cref="Normalizar"/>.
    /// </summary>
    public static List<string> QuitarEncabezadosYPies(List<string> textosDePagina)
    {
        var lineasDePaginas = new List<string[]>();
        var paginasConLaLineaArriba = new Dictionary<string, int>();
        var paginasConLaLineaAbajo = new Dictionary<string, int>();
        foreach (var texto in textosDePagina)
        {
            var lineas = texto.Split('\n');
            lineasDePaginas.Add(lineas);
            var posiciones = BuscarLineasDeContenido(lineas);
            ContarLineasDelBorde(lineas, posiciones, 0, LineasRevisadasArriba, paginasConLaLineaArriba);
            ContarLineasDelBorde(lineas, posiciones, posiciones.Count - LineasRevisadasAbajo, LineasRevisadasAbajo,
                paginasConLaLineaAbajo);
        }

        var textosLimpios = new List<string>();
        foreach (var lineas in lineasDePaginas)
        {
            var posiciones = BuscarLineasDeContenido(lineas);
            var quitar = new HashSet<int>();
            for (var indice = 0; indice < lineas.Length; indice++)
            {
                if (PatronEtiquetaDeFigura.IsMatch(lineas[indice].Trim()))
                {
                    quitar.Add(indice);
                }
            }

            for (var orden = 0; orden < posiciones.Count && orden < LineasRevisadasArriba; orden++)
            {
                var clave = CrearClaveDeLinea(lineas[posiciones[orden]]);
                var esRepetida = paginasConLaLineaArriba.GetValueOrDefault(clave) >= PaginasMinimasParaSerRepetida;
                if (!esRepetida || PalabrasDeAvisoQueSeConservanArriba.Contains(clave.TrimEnd(':'))
                    || EsProsa(lineas[posiciones[orden]]))
                {
                    break;
                }

                quitar.Add(posiciones[orden]);
            }

            for (var orden = posiciones.Count - 1; orden >= 0 && orden >= posiciones.Count - LineasRevisadasAbajo; orden--)
            {
                var clave = CrearClaveDeLinea(lineas[posiciones[orden]]);
                var esRepetida = paginasConLaLineaAbajo.GetValueOrDefault(clave) >= PaginasMinimasParaSerRepetida;
                if (!esRepetida || quitar.Contains(posiciones[orden]) || EsProsa(lineas[posiciones[orden]]))
                {
                    break;
                }

                quitar.Add(posiciones[orden]);
            }

            var lineasQueQuedan = new List<string>();
            for (var indice = 0; indice < lineas.Length; indice++)
            {
                if (!quitar.Contains(indice))
                {
                    lineasQueQuedan.Add(lineas[indice]);
                }
            }

            textosLimpios.Add(string.Join("\n", lineasQueQuedan));
        }

        return textosLimpios;
    }

    // Posiciones de las líneas con texto que no son etiquetas de figura: los bordes se miden sobre ellas.
    private static List<int> BuscarLineasDeContenido(string[] lineas)
    {
        var posiciones = new List<int>();
        for (var indice = 0; indice < lineas.Length; indice++)
        {
            var linea = lineas[indice].Trim();
            if (linea.Length > 0 && !PatronEtiquetaDeFigura.IsMatch(linea))
            {
                posiciones.Add(indice);
            }
        }

        return posiciones;
    }

    private static void ContarLineasDelBorde(string[] lineas, List<int> posiciones, int desde, int cantidad,
        Dictionary<string, int> paginasConLaLinea)
    {
        // Cada línea cuenta una vez por página, aunque se repita dentro de ella.
        var clavesDeEstaPagina = new HashSet<string>();
        for (var orden = Math.Max(desde, 0); orden < posiciones.Count && orden < desde + cantidad; orden++)
        {
            clavesDeEstaPagina.Add(CrearClaveDeLinea(lineas[posiciones[orden]]));
        }

        foreach (var clave in clavesDeEstaPagina)
        {
            paginasConLaLinea[clave] = paginasConLaLinea.GetValueOrDefault(clave) + 1;
        }
    }

    // Una advertencia que el manual repite al pie de varias páginas se repite igual que un pie, pero es texto
    // corrido: tiene tres palabras seguidas en minúscula, o empieza en minúscula y cierra la oración ("death.").
    // Un encabezado o un pie es un título corto, un número o una dirección.
    private static bool EsProsa(string linea)
    {
        var texto = linea.Trim();
        if (texto.Length == 0)
        {
            return false;
        }

        var empiezaEnMinuscula = char.IsLower(texto[0]);
        var cierraLaOracion = ".!?;:".Contains(texto[^1]);
        if (empiezaEnMinuscula && cierraLaOracion && texto.Length > 1)
        {
            return true;
        }

        var seguidasEnMinuscula = 0;
        foreach (var palabra in texto.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            seguidasEnMinuscula = char.IsLower(palabra[0]) ? seguidasEnMinuscula + 1 : 0;
            if (seguidasEnMinuscula == 3)
            {
                return true;
            }
        }

        return false;
    }

    // Sin espacios ni mayúsculas y con los números de página igualados: el extractor a veces pega o separa palabras
    // del encabezado ("FunctionsSY365" en una página, "Functions SY365" en otra).
    private static string CrearClaveDeLinea(string linea)
    {
        var sinNumerosDePagina = PatronNumeroDePagina.Replace(linea.Trim(), "#");
        return PatronEspacios.Replace(sinNumerosDePagina, "").ToLowerInvariant();
    }

    public static List<string> Dividir(string texto)
    {
        var fragmentos = new List<string>();
        var inicio = 0;
        while (inicio < texto.Length)
        {
            var fin = BuscarFinDelFragmento(texto, inicio);

            var fragmento = texto.Substring(inicio, fin - inicio).Trim();
            if (fragmento.Length > 0)
            {
                fragmentos.Add(fragmento);
            }

            var llegoAlFinalDelTexto = fin == texto.Length;
            if (llegoAlFinalDelTexto)
            {
                break;
            }

            inicio = BuscarInicioDelSiguienteFragmento(texto, inicio, fin);
        }

        return fragmentos;
    }

    private static int BuscarFinDelFragmento(string texto, int inicio)
    {
        var finMaximo = inicio + LargoMaximoDeFragmento;
        if (finMaximo >= texto.Length)
        {
            return texto.Length;
        }

        var ultimoEspacio = texto.LastIndexOf(' ', finMaximo - 1, finMaximo - inicio);
        var cortarEnElEspacioDejaUnFragmentoLargo = ultimoEspacio > inicio + LargoMinimoDeFragmento;
        if (cortarEnElEspacioDejaUnFragmentoLargo)
        {
            return ultimoEspacio;
        }

        return finMaximo;
    }

    private static int BuscarInicioDelSiguienteFragmento(string texto, int inicio, int fin)
    {
        var inicioConRepeticion = fin - CaracteresRepetidosEntreFragmentos;
        var siguienteInicio = inicioConRepeticion;
        if (siguienteInicio <= inicio)
        {
            siguienteInicio = inicio + 1;
        }

        var siguienteEspacio = texto.IndexOf(' ', siguienteInicio);
        var hayUnEspacioAntesDelFin = siguienteEspacio >= siguienteInicio && siguienteEspacio < fin;
        if (hayUnEspacioAntesDelFin)
        {
            return siguienteEspacio + 1;
        }

        return siguienteInicio;
    }

    /// <summary>
    /// Encierra entre marcas los títulos que ocupan una línea propia: los numerados ("4.2 Cambio del filtro"), los
    /// escritos en mayúsculas ("500 HOURS OF SERVICE") y los que encabezan un procedimiento ("Primary Fuel Filter"
    /// seguido de "1. ..."). Hay que hacerlo antes de <see cref="Normalizar"/>, que une las líneas: después ya no se
    /// sabe dónde termina el título.
    /// </summary>
    public static string MarcarTitulosNumerados(string texto)
    {
        var lineas = texto.Split('\n');
        var marcadas = new string[lineas.Length];
        for (var indice = 0; indice < lineas.Length; indice++)
        {
            var linea = lineas[indice].Trim();
            var siguiente = BuscarLineaSiguiente(lineas, indice);
            var esTitulo = EsTituloNumerado(linea, siguiente) || EsTituloEnMayusculas(linea)
                || EsTituloDeProcedimiento(linea, siguiente);
            marcadas[indice] = esTitulo
                ? TextoDelManual.MarcaDeTituloNumerado + linea + TextoDelManual.MarcaDeTituloNumerado
                : lineas[indice];
        }

        return string.Join("\n", marcadas);
    }

    private static string BuscarLineaSiguiente(string[] lineas, int indice)
    {
        for (var siguiente = indice + 1; siguiente < lineas.Length; siguiente++)
        {
            var linea = lineas[siguiente].Trim();
            if (linea.Length > 0)
            {
                return linea;
            }
        }

        return "";
    }

    // Un paso numerado largo se parte en varias líneas ("2. Follow the Lock-out Tag-out procedure in the Safety" /
    // "section of this manual."): la primera parece un título, pero la oración sigue en la línea siguiente.
    private static bool EsTituloNumerado(string linea, string siguiente)
    {
        if (!PatronLineaDeTituloNumerado.IsMatch(linea))
        {
            return false;
        }

        var palabras = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return palabras.Length - 1 <= PalabrasMaximasDeTituloNumerado && !ContinuaEnLaLineaSiguiente(linea, siguiente);
    }

    private static bool EsTituloEnMayusculas(string linea)
    {
        if (!PatronLineaDeTituloSinNumero.IsMatch(linea) || linea.ToUpperInvariant() != linea)
        {
            return false;
        }

        var palabras = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var letras = 0;
        foreach (var caracter in linea)
        {
            if (char.IsLetter(caracter))
            {
                letras++;
            }
        }

        // Los códigos de figura ("TS1671 –UN–01MAY03") van en mayúsculas pero casi no tienen palabras.
        var palabrasDeSoloLetras = 0;
        foreach (var palabra in palabras)
        {
            if (PalabrasDeAviso.Contains(palabra))
            {
                return false;
            }

            if (palabra.Length >= 2 && palabra.All(char.IsLetter))
            {
                palabrasDeSoloLetras++;
            }
        }

        return palabrasDeSoloLetras >= PalabrasMinimasDeTituloSinNumero && palabras.Length <= PalabrasMaximasDeTituloSinNumero
            && letras >= LetrasMinimasDeTituloEnMayusculas && !ContinuaEnLaLineaSiguiente(linea, "");
    }

    private static bool EsTituloDeProcedimiento(string linea, string siguiente)
    {
        if (!PatronLineaDeTituloSinNumero.IsMatch(linea) || !PatronInicioDeProcedimiento.IsMatch(siguiente))
        {
            return false;
        }

        var palabras = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return palabras.Length >= PalabrasMinimasDeTituloSinNumero && palabras.Length <= PalabrasMaximasDeTituloSinNumero
            && !ContinuaEnLaLineaSiguiente(linea, siguiente);
    }

    // Una referencia a la figura al principio de la línea ("(1) leftward with...") no cuenta: se mira lo que sigue.
    private static readonly Regex PatronReferenciaAlInicio = new("^\\(\\w{1,3}\\)\\s*");

    private static bool ContinuaEnLaLineaSiguiente(string linea, string siguiente)
    {
        var textoSiguiente = PatronReferenciaAlInicio.Replace(siguiente, "");
        var siguienteEmpiezaEnMinuscula = textoSiguiente.Length > 0 && char.IsLower(textoSiguiente[0]);
        var palabras = linea.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var terminaEnConector = palabras.Length > 0 && ConectoresAlFinalDeLinea.Contains(palabras[^1]);
        var terminaCortada = linea.EndsWith('/') || linea.EndsWith('-');
        return siguienteEmpiezaEnMinuscula || terminaEnConector || terminaCortada;
    }

    public static string Normalizar(string texto)
    {
        var normalizado = PatronCaracteresDeControl.Replace(texto, "");
        normalizado = normalizado.Normalize(NormalizationForm.FormKC);
        normalizado = normalizado.Replace(GuionInvisible, "");
        return PatronEspacios.Replace(normalizado, " ").Trim();
    }
}

public class FragmentoDocumento
{
    public int Pagina { get; set; }
    public string Texto { get; set; }

    public FragmentoDocumento(int pagina, string texto)
    {
        Pagina = pagina;
        Texto = texto;
    }
}

public class FuenteDocumento
{
    public long Id { get; set; }
    public int Pagina { get; set; }
    public string Texto { get; set; }
    public string Documento { get; set; }

    public FuenteDocumento(long id, int pagina, string texto, string documento)
    {
        Id = id;
        Pagina = pagina;
        Texto = texto;
        Documento = documento;
    }
}

public class FuentePuntuada
{
    public FuenteDocumento Fuente { get; set; }
    public float Puntaje { get; set; }

    public FuentePuntuada(FuenteDocumento fuente, float puntaje)
    {
        Fuente = fuente;
        Puntaje = puntaje;
    }
}

public class SeccionDocumento
{
    public long FragmentoId { get; set; }
    public int Pagina { get; set; }
    public string Documento { get; set; }
    public string Titulo { get; set; }
    public string Contexto { get; set; }
    public string Cuerpo { get; set; }
    public bool LlegaAlFinal { get; set; }

    public SeccionDocumento(long fragmentoId, int pagina, string documento, string titulo, string contexto, string cuerpo, bool llegaAlFinal)
    {
        FragmentoId = fragmentoId;
        Pagina = pagina;
        Documento = documento;
        Titulo = titulo;
        Contexto = contexto;
        Cuerpo = cuerpo;
        LlegaAlFinal = llegaAlFinal;
    }
}

public class TituloEncontrado
{
    public int Inicio { get; set; }
    public int Fin { get; set; }
    public string Texto { get; set; }

    public TituloEncontrado(int inicio, int fin, string texto)
    {
        Inicio = inicio;
        Fin = fin;
        Texto = texto;
    }
}

public class ArchivoGuardado
{
    public byte[] Contenido { get; set; }
    public string NombreArchivo { get; set; }

    public ArchivoGuardado(byte[] contenido, string nombreArchivo)
    {
        Contenido = contenido;
        NombreArchivo = nombreArchivo;
    }
}
