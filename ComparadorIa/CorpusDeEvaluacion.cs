using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Api.Servicios;

namespace ComparadorIa;

/// <summary>
/// Arma las secciones del corpus con el mismo extractor y divisor que usa la API, sin base vectorial ni embeddings:
/// la comparación mide solo la redacción, así que todos los proveedores reciben exactamente la misma sección.
/// </summary>
public static class CorpusDeEvaluacion
{
    private const int LetrasDeRaiz = 5;
    private const int PesoDelTitulo = 3;

    private static readonly Regex PatronSeparador = new("[^a-z0-9]+");

    public static List<SeccionDocumento> LeerSecciones(string carpeta)
    {
        var fuentes = new List<FuenteDocumento>();
        long siguienteId = 1;

        var archivos = Directory.GetFiles(carpeta, "*.pdf");
        Array.Sort(archivos, StringComparer.Ordinal);
        foreach (var archivo in archivos)
        {
            var nombre = Path.GetFileName(archivo);
            foreach (var fragmento in DocumentoPdf.Extraer(File.ReadAllBytes(archivo)))
            {
                fuentes.Add(new FuenteDocumento(siguienteId, fragmento.Pagina, fragmento.Texto, nombre));
                siguienteId++;
            }
        }

        return SeccionesCandidatas.Armar(fuentes);
    }

    public static SeccionDocumento? ElegirSeccion(CasoDeEvaluacion caso, List<SeccionDocumento> secciones)
    {
        var candidatas = secciones;
        if (!string.IsNullOrWhiteSpace(caso.Seccion))
        {
            var pista = Normalizar(caso.Seccion);
            candidatas = new List<SeccionDocumento>();
            foreach (var seccion in secciones)
            {
                if (Normalizar(seccion.Titulo).Contains(pista))
                {
                    candidatas.Add(seccion);
                }
            }

            if (candidatas.Count == 0)
            {
                candidatas = secciones;
            }
        }

        var raicesDeLaConsulta = ObtenerRaices(caso.Consulta);
        SeccionDocumento? elegida = null;
        var mejorPuntaje = 0;
        foreach (var seccion in candidatas)
        {
            var raicesDelTitulo = ObtenerRaices(seccion.Titulo);
            var raicesDelTexto = ObtenerRaices(seccion.Contexto + " " + seccion.Cuerpo);
            var puntaje = 0;
            foreach (var raiz in raicesDeLaConsulta)
            {
                if (raicesDelTitulo.Contains(raiz))
                {
                    puntaje += PesoDelTitulo;
                }

                if (raicesDelTexto.Contains(raiz))
                {
                    puntaje++;
                }
            }

            if (puntaje > mejorPuntaje)
            {
                mejorPuntaje = puntaje;
                elegida = seccion;
            }
        }

        return elegida;
    }

    /// <summary>
    /// Busca el valor sin distinguir mayúsculas, espacios repetidos, coma o punto decimal, ni la variante de guion
    /// (los modelos escriben a veces "15W‑40" con guion no separable).
    /// </summary>
    public static bool ContieneValor(string texto, string valor)
    {
        var textoComparable = PrepararParaComparar(texto);
        var valorComparable = PrepararParaComparar(valor);
        return textoComparable.Contains(valorComparable);
    }

    private static string PrepararParaComparar(string texto)
    {
        var resultado = Regex.Replace(texto.ToLowerInvariant(), "\\s+", " ");
        resultado = Regex.Replace(resultado, "[\\u2010-\\u2015\\u2212]", "-");
        return Regex.Replace(resultado, "(\\d),(\\d)", "$1.$2");
    }

    private static HashSet<string> ObtenerRaices(string texto)
    {
        var raices = new HashSet<string>();
        foreach (var palabra in Normalizar(texto).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (palabra.Length < 3)
            {
                continue;
            }

            raices.Add(palabra.Length > LetrasDeRaiz ? palabra.Substring(0, LetrasDeRaiz) : palabra);
        }

        return raices;
    }

    private static string Normalizar(string texto)
    {
        var descompuesto = texto.ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sinTildes = new StringBuilder();
        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                sinTildes.Append(caracter);
            }
        }

        return PatronSeparador.Replace(sinTildes.ToString(), " ").Trim();
    }
}
