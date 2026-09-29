using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Api.Servicios;

public static class SeccionesCandidatas
{
    public const int LargoMinimoDeSeccion = 80;
    private const int MaximoDeOpcionesEnAclaracion = 5;
    private const int LetrasDeRaiz = 5;

    private static readonly string[] PalabrasVacias =
    {
        "del", "las", "los", "con", "sin", "por", "para", "una", "unos", "unas", "que", "sus",
        "como", "cada", "este", "esta", "estos", "estas", "entre", "sobre", "hacia", "desde"
    };

    private static readonly Regex PatronSeparadorDePalabras = new("[^a-z0-9]+");

    public static List<SeccionDocumento> Armar(List<FuenteDocumento> fuentes)
    {
        var secciones = UnirSeccionesPartidas(fuentes);
        secciones = QuitarRepetidas(secciones);
        secciones = QuitarMuyCortas(secciones);
        return secciones;
    }

    public static List<SeccionDocumento> BuscarOpcionesEquivalentes(string consulta,
        List<SeccionDocumento> relevantes)
    {
        var principal = relevantes[0];
        var opciones = new List<SeccionDocumento> { principal };
        if (principal.Titulo.Length == 0)
        {
            return opciones;
        }

        var raicesDeLaConsulta = ObtenerRaices(consulta);
        var raicesDelTituloPrincipal = ObtenerRaices(principal.Titulo);
        var raicesCompartidas = new List<string>();
        foreach (var raiz in raicesDeLaConsulta)
        {
            if (raicesDelTituloPrincipal.Contains(raiz))
            {
                raicesCompartidas.Add(raiz);
            }
        }

        if (raicesCompartidas.Count == 0)
        {
            return opciones;
        }

        for (var indice = 1; indice < relevantes.Count; indice++)
        {
            var seccion = relevantes[indice];
            if (seccion.Titulo.Length == 0)
            {
                continue;
            }

            var raicesDelTitulo = ObtenerRaices(seccion.Titulo);
            var tieneTodasLasRaices = true;
            foreach (var raiz in raicesCompartidas)
            {
                if (!raicesDelTitulo.Contains(raiz))
                {
                    tieneTodasLasRaices = false;
                }
            }

            if (tieneTodasLasRaices)
            {
                opciones.Add(seccion);
            }

            if (opciones.Count == MaximoDeOpcionesEnAclaracion)
            {
                break;
            }
        }

        return opciones;
    }

    private static List<SeccionDocumento> UnirSeccionesPartidas(List<FuenteDocumento> fuentes)
    {
        var secciones = new List<SeccionDocumento>();
        var posicionSeccionAbierta = -1;

        var fuentesOrdenadas = new List<FuenteDocumento>(fuentes);
        fuentesOrdenadas.Sort(CompararPorId);

        foreach (var fuente in fuentesOrdenadas)
        {
            var seccionesDelFragmento = TextoDelManual.DividirEnSecciones(fuente);
            for (var indice = 0; indice < seccionesDelFragmento.Count; indice++)
            {
                var seccion = seccionesDelFragmento[indice];
                var esElPrimerTrozo = indice == 0;
                var noTieneTitulo = seccion.Titulo.Length == 0;
                var hayUnaSeccionAbierta = posicionSeccionAbierta != -1;
                var esContinuacion = false;
                if (esElPrimerTrozo && noTieneTitulo && hayUnaSeccionAbierta)
                {
                    esContinuacion = EsFragmentoSiguiente(secciones[posicionSeccionAbierta], fuente);
                }

                if (esContinuacion)
                {
                    var abierta = secciones[posicionSeccionAbierta];
                    var cuerpoUnido = TextoDelManual.UnirContinuacion(abierta.Cuerpo, seccion.Cuerpo);
                    var seccionUnida = new SeccionDocumento(fuente.Id, abierta.Pagina, abierta.Documento,
                        abierta.Titulo, abierta.Contexto, cuerpoUnido, seccion.LlegaAlFinal);
                    secciones[posicionSeccionAbierta] = seccionUnida;
                }
                else
                {
                    secciones.Add(seccion);
                    posicionSeccionAbierta = secciones.Count - 1;
                }

                var quedaAbierta = seccion.LlegaAlFinal && secciones[posicionSeccionAbierta].Titulo.Length > 0;
                if (!quedaAbierta)
                {
                    posicionSeccionAbierta = -1;
                }
            }
        }

        return secciones;
    }

    private static int CompararPorId(FuenteDocumento primera, FuenteDocumento segunda)
    {
        return primera.Id.CompareTo(segunda.Id);
    }

    private static bool EsFragmentoSiguiente(SeccionDocumento seccionAbierta, FuenteDocumento fuente)
    {
        var esElSiguienteNumero = fuente.Id == seccionAbierta.FragmentoId + 1;
        var esElMismoDocumento = fuente.Documento == seccionAbierta.Documento;
        return esElSiguienteNumero && esElMismoDocumento;
    }

    private static List<SeccionDocumento> QuitarRepetidas(List<SeccionDocumento> secciones)
    {
        var resultado = new List<SeccionDocumento>();
        var claves = new List<string>();

        foreach (var seccion in secciones)
        {
            var clave = "fragmento " + seccion.FragmentoId;
            if (seccion.Titulo.Length > 0)
            {
                clave = seccion.Documento + " " + CrearClave(seccion.Titulo);
            }

            var posicion = claves.IndexOf(clave);
            if (posicion == -1)
            {
                claves.Add(clave);
                resultado.Add(seccion);
            }
            else if (seccion.Cuerpo.Length > resultado[posicion].Cuerpo.Length)
            {
                resultado[posicion] = seccion;
            }
        }

        return resultado;
    }

    private static List<SeccionDocumento> QuitarMuyCortas(List<SeccionDocumento> secciones)
    {
        var resultado = new List<SeccionDocumento>();
        foreach (var seccion in secciones)
        {
            if (seccion.Cuerpo.Length >= LargoMinimoDeSeccion)
            {
                resultado.Add(seccion);
            }
        }

        return resultado;
    }

    private static string CrearClave(string texto)
    {
        var sinTildes = QuitarTildes(texto.ToLowerInvariant());
        return PatronSeparadorDePalabras.Replace(sinTildes, " ").Trim();
    }

    private static List<string> ObtenerRaices(string texto)
    {
        var raices = new List<string>();
        foreach (var palabra in CrearClave(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (palabra.Length < 3 || PalabrasVacias.Contains(palabra))
            {
                continue;
            }

            var raiz = palabra;
            if (raiz.Length > LetrasDeRaiz)
            {
                raiz = raiz.Substring(0, LetrasDeRaiz);
            }

            if (!raices.Contains(raiz))
            {
                raices.Add(raiz);
            }
        }

        return raices;
    }

    private static string QuitarTildes(string texto)
    {
        var descompuesto = texto.Normalize(NormalizationForm.FormD);
        var resultado = new StringBuilder();
        foreach (var caracter in descompuesto)
        {
            var esTilde = CharUnicodeInfo.GetUnicodeCategory(caracter) == UnicodeCategory.NonSpacingMark;
            if (!esTilde)
            {
                resultado.Append(caracter);
            }
        }

        return resultado.ToString().Normalize(NormalizationForm.FormC);
    }
}
