using System.Text.RegularExpressions;

namespace Api.Servicios;

/// <summary>
/// Intervalos de mantenimiento por horas ("500 horas", "1,000 HOURS OF SERVICE"). El reranker y los embeddings casi no
/// distinguen 50 de 500, y un técnico que pide uno no puede recibir otro: una sección que nombra un intervalo se elige o
/// se descarta comparando el número.
/// </summary>
public static class IntervaloDeHoras
{
    // Caracteres del principio de una sección sin título en los que se busca "cada N horas": el manual John Deere abre
    // con "Mantenimiento—Cada 10 horas" sin un título reconocible.
    private const int CaracteresDelCuerpoSinTitulo = 80;

    private const string PalabraDeHoras = "(?:(?:service|de\\s+servicio)\\s+)?(?:hours?|horas?|hrs?|hs|h)\\b";

    // Solo los miles seguidos de horas: "1,500 kg" o "1.500 kPa" son valores del manual y no se tocan.
    private static readonly Regex PatronMilesDeHoras = new(
        "(?<![\\d.,])(\\d{1,2})[,.](\\d{3})(?=\\s*" + PalabraDeHoras + ")", RegexOptions.IgnoreCase);

    private static readonly Regex PatronIntervalo = new(
        "(?<![\\d.,])(\\d{1,5})\\s*" + PalabraDeHoras, RegexOptions.IgnoreCase);

    // En el texto sin título un número de horas solo no indica un intervalo: una tabla puede decir "100 hours greasing
    // 500 hours greasing". Hace falta "cada" o "every".
    private static readonly Regex PatronCadaHoras = new(
        "\\b(?:cada|every)\\s+\\d{1,5}\\s*" + PalabraDeHoras, RegexOptions.IgnoreCase);

    /// <summary>Convierte "1,000 hours" y "1.000 horas" en "1000 hours" y "1000 horas".</summary>
    public static string NormalizarMiles(string texto)
    {
        return PatronMilesDeHoras.Replace(texto, "$1$2");
    }

    public static HashSet<int> Extraer(string texto)
    {
        var intervalos = new HashSet<int>();
        foreach (Match coincidencia in PatronIntervalo.Matches(NormalizarMiles(texto)))
        {
            intervalos.Add(int.Parse(coincidencia.Groups[1].Value));
        }

        return intervalos;
    }

    /// <summary>La sección nombra alguno de los intervalos pedidos.</summary>
    public static bool Coincide(SeccionDocumento seccion, HashSet<int> intervalosPedidos)
    {
        return Extraer(Etiqueta(seccion)).Overlaps(intervalosPedidos);
    }

    /// <summary>
    /// Quita las secciones que nombran otro intervalo que el pedido. Las que no nombran ninguno se conservan (por
    /// ejemplo, "Intervalos de mantenimiento"): el reranker decide si responden.
    /// </summary>
    public static List<SeccionDocumento> QuitarOtrosIntervalos(List<SeccionDocumento> secciones,
        HashSet<int> intervalosPedidos)
    {
        if (intervalosPedidos.Count == 0)
        {
            return secciones;
        }

        var conservadas = new List<SeccionDocumento>();
        foreach (var seccion in secciones)
        {
            var intervalosDeLaSeccion = Extraer(Etiqueta(seccion));
            if (intervalosDeLaSeccion.Count == 0 || intervalosDeLaSeccion.Overlaps(intervalosPedidos))
            {
                conservadas.Add(seccion);
            }
        }

        return conservadas;
    }

    private static string Etiqueta(SeccionDocumento seccion)
    {
        var etiqueta = seccion.Titulo + " " + seccion.Contexto;
        if (seccion.Titulo.Length == 0)
        {
            // Un trozo sin título pertenece a la sección en curso: la continuación de "250 Hours of service" no
            // responde a quien pidió 1000 horas.
            var largo = Math.Min(CaracteresDelCuerpoSinTitulo, seccion.Cuerpo.Length);
            var principio = PatronCadaHoras.Match(seccion.Cuerpo.Substring(0, largo));
            etiqueta += " " + seccion.SeccionEnCurso + " " + principio.Value;
        }

        return etiqueta;
    }
}
