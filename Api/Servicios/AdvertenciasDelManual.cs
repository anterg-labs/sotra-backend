using System.Text.RegularExpressions;

namespace Api.Servicios;

public static class AdvertenciasDelManual
{
    private const int MaximoDeAdvertencias = 4;
    private const int LargoMaximoDeAdvertencia = 350;
    private const int LargoMinimoDeVinetaDeAdvertencia = 60;

    private static readonly Regex PatronEspacios = new("\\s+");

    private static readonly Regex PatronAdvertencia = new(
        "\\b(?:ADVERTENCIA|PRECAUCIÓN|PRECAUCION|PELIGRO|AVISO|IMPORTANTE)\\b(.*?)" +
        "(?=\\s\\d{1,2}[.)]\\s|¶|\\b(?:ADVERTENCIA|PRECAUCIÓN|PRECAUCION|PELIGRO|AVISO|IMPORTANTE|NOTA|COMENTARIO)\\b|$)");

    private static readonly Regex PatronPalabraDeAdvertencia = new(
        "\\b(?:ADVERTENCIA|PRECAUCIÓN|PRECAUCION|PELIGRO|AVISO|IMPORTANTE)\\b");

    private static readonly Regex PatronPaginaDeReferenciaCruzada = new("\\(\\d+-\\d+\\)");

    public static List<string> Extraer(string cuerpo)
    {
        var advertencias = new List<string>();

        foreach (var textoAdvertencia in BuscarTextosDeAdvertencia(cuerpo))
        {
            foreach (var parte in textoAdvertencia.Split('•'))
            {
                var textoLimpio = TextoDelManual.QuitarMarcasInternas(parte);
                var advertencia = TextoDelManual.RecortarEnOracion(textoLimpio, LargoMaximoDeAdvertencia);
                var esReferenciaCruzada = PatronPaginaDeReferenciaCruzada.IsMatch(advertencia);
                var yaEstaAgregada = advertencias.Contains(advertencia);
                if (advertencia.Length > 0 && !esReferenciaCruzada && !yaEstaAgregada)
                {
                    advertencias.Add(advertencia);
                }

                if (advertencias.Count == MaximoDeAdvertencias)
                {
                    return advertencias;
                }
            }
        }

        return advertencias;
    }

    public static string Quitar(string cuerpo)
    {
        var cuerpoSinAdvertencias = cuerpo;
        foreach (var textoAdvertencia in BuscarTextosDeAdvertencia(cuerpo))
        {
            cuerpoSinAdvertencias = cuerpoSinAdvertencias.Replace(textoAdvertencia, " ");
        }

        cuerpoSinAdvertencias = PatronPalabraDeAdvertencia.Replace(cuerpoSinAdvertencias, " ");
        return PatronEspacios.Replace(cuerpoSinAdvertencias, " ").Trim();
    }

    private static List<string> BuscarTextosDeAdvertencia(string cuerpo)
    {
        var textos = new List<string>();
        foreach (Match coincidencia in PatronAdvertencia.Matches(cuerpo))
        {
            var textoCompleto = coincidencia.Groups[1].Value.Trim();
            var fin = BuscarFinDeAdvertencia(textoCompleto);
            var textoAdvertencia = textoCompleto.Substring(0, fin).Trim();
            if (textoAdvertencia.Length > 0)
            {
                textos.Add(textoAdvertencia);
            }
        }

        return textos;
    }

    private static int BuscarFinDeAdvertencia(string texto)
    {
        if (!texto.StartsWith("•"))
        {
            var primeraVineta = texto.IndexOf('•');
            if (primeraVineta > 0)
            {
                return primeraVineta;
            }
            return texto.Length;
        }

        var posicion = texto.IndexOf('•', 1);
        while (posicion > 0)
        {
            var siguiente = texto.IndexOf('•', posicion + 1);
            var finVineta = texto.Length;
            if (siguiente > 0)
            {
                finVineta = siguiente;
            }

            var vineta = texto.Substring(posicion + 1, finVineta - posicion - 1).Trim();
            var pareceUnDato = vineta.Contains(':') || vineta.Length < LargoMinimoDeVinetaDeAdvertencia;
            if (pareceUnDato)
            {
                return posicion;
            }

            posicion = siguiente;
        }

        return texto.Length;
    }
}
