using System.Text.RegularExpressions;

namespace Api.Servicios;

public static class RedaccionDeSugerencia
{
    private const int LargoMaximoDeTextoParaElModelo = 3000;
    private const int LargoMinimoDeResumen = 20;
    private const int LargoMaximoDeResumen = 1200;

    private const int LargoMaximoDeTitulo = 100;
    private const int PalabrasMaximasDeTitulo = 12;
    private const string PrefijoDelTitulo = "Título:";

    private static readonly Regex PatronNumero = new("\\d+(?:[.,]\\d+)?");

    // El modelo a veces resalta el rótulo con asteriscos o lo escribe sin tilde.
    private static readonly Regex PatronLineaDeTitulo = new("^\\W*t[ií]tulo\\W*:\\W*(.*?)\\W*$", RegexOptions.IgnoreCase);

    // Algunas veces el modelo también rotula el resumen ("Resumen: Se debe...").
    private static readonly Regex PatronRotuloDeResumen = new("^[*_\\s]*resumen[*_\\s]*:[*_\\s]*", RegexOptions.IgnoreCase);

    public const string Instrucciones =
        "Resumís secciones de manuales técnicos para técnicos de mantenimiento. " +
        "Reglas obligatorias: " +
        "1) Usá únicamente la información del texto del manual que se te entrega. " +
        "2) No agregues pasos, datos, causas ni recomendaciones que no estén en el texto. " +
        "3) Copiá exactamente los números, unidades, intervalos, cantidades, medidas y nombres de componentes. " +
        "Mantené cada condición y su acción tal como las expresa el manual: " +
        "no cambies el verbo de la acción ni conviertas una acción en otra distinta. " +
        "Conservá las obligaciones y prohibiciones como tales: si el manual dice \"use\", \"debe\" o \"nunca\", " +
        "no lo conviertas en recomendación ni en algo opcional. " +
        "4) En la primera línea escribí \"" + PrefijoDelTitulo + "\" y un título breve en español neutro para la " +
        "tarea, de 3 a 10 palabras y sin punto final, traducido si el manual está en otro idioma. " +
        "En las líneas siguientes escribí el resumen en español neutro, de 2 a 4 oraciones, en prosa, sin listas. " +
        "Usá forma impersonal o de usted (por ejemplo: \"se debe\", \"retire\", \"coloque\"), nunca de tú. " +
        "5) Si el texto o el contexto previo indican un intervalo, un momento o una condición para realizar " +
        "la tarea, empezá por eso, con la misma acción que indica el manual. Si no hay intervalo ni condición, " +
        "empezá directamente por la acción principal. No empieces el resumen repitiendo el título. " +
        "6) Si el texto indica capacidades, cantidades o valores de ajuste, incluilos. " +
        "7) No repitas advertencias de seguridad; se muestran aparte. " +
        "8) Si el texto describe pasos, resumí los principales en orden. " +
        "9) No menciones páginas, secciones ni el nombre del manual. " +
        "10) Si el manual está en otro idioma, traducí con el vocabulario técnico de mantenimiento en español: " +
        "por ejemplo, \"oil it\" es \"aceitarlo\", \"unscrew\" es \"desenroscar\", \"drain pan\" es " +
        "\"bandeja de drenaje\" y \"bolt\" es \"perno\". Usá solo palabras que existan en español; si no estás " +
        "seguro de cómo se traduce un término técnico, escribilo en español y agregá el original entre paréntesis. " +
        "11) Si el texto del manual no tiene ninguna información sobre la tarea consultada (por ejemplo, es un " +
        "índice o trata otro tema), respondé únicamente " + RespuestaSinInformacion + ", sin título ni resumen.";

    /// <summary>Lo que responde el modelo cuando la sección elegida no trata la tarea consultada.</summary>
    public const string RespuestaSinInformacion = "SIN_INFORMACION";

    public static bool EsRespuestaSinInformacion(string respuesta)
    {
        return respuesta.Contains(RespuestaSinInformacion, StringComparison.OrdinalIgnoreCase);
    }

    public static string CrearMensajeParaElModelo(string consulta, SeccionDocumento seccion, string textoParaResumir)
    {
        var textoDelManual = TextoDelManual.QuitarMarcasInternas(textoParaResumir);
        if (textoDelManual.Length > LargoMaximoDeTextoParaElModelo)
        {
            textoDelManual = textoDelManual.Substring(0, LargoMaximoDeTextoParaElModelo);
        }

        var mensaje = "Consulta del técnico: " + consulta + "\n\n";
        mensaje += "Sección del manual: " + TextoDelManual.CrearTituloLegible(seccion.Titulo) + "\n";
        if (seccion.Contexto.Length > 0)
        {
            mensaje += "Contexto previo de la sección: " + seccion.Contexto + "\n";
        }

        mensaje += "\nTexto del manual:\n" + textoDelManual + "\n\nEscribí el resumen.";
        return mensaje;
    }

    /// <summary>
    /// Separa el título de la primera línea ("Título: ...") del resumen. Si no hay título o no es un título breve,
    /// devuelve el título vacío y todo el texto como resumen.
    /// </summary>
    public static RespuestaDelModelo SepararTitulo(string respuesta)
    {
        var texto = respuesta.Trim();
        var finDePrimeraLinea = texto.IndexOf('\n');
        var primeraLinea = finDePrimeraLinea == -1 ? texto : texto.Substring(0, finDePrimeraLinea);
        var coincidencia = PatronLineaDeTitulo.Match(primeraLinea.Trim());
        if (!coincidencia.Success || finDePrimeraLinea == -1)
        {
            return new RespuestaDelModelo("", texto);
        }

        var titulo = coincidencia.Groups[1].Value.Trim();
        var resumen = PatronRotuloDeResumen.Replace(texto.Substring(finDePrimeraLinea + 1).Trim(), "");
        var palabras = titulo.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        if (titulo.Length == 0 || titulo.Length > LargoMaximoDeTitulo || palabras > PalabrasMaximasDeTitulo)
        {
            return new RespuestaDelModelo("", resumen);
        }

        return new RespuestaDelModelo(titulo, resumen);
    }

    /// <summary>
    /// El resumen debe tener un largo razonable y, junto con el título, no puede traer números que no estén en el
    /// manual ni en la consulta: un número inventado en un procedimiento técnico es peor que no resumir.
    /// </summary>
    public static bool EsResumenConfiable(string resumen, string consulta, SeccionDocumento seccion, string titulo = "")
    {
        if (resumen.Length < LargoMinimoDeResumen || resumen.Length > LargoMaximoDeResumen)
        {
            return false;
        }

        var textoDelManual = seccion.Titulo + " " + seccion.Contexto + " " + seccion.Cuerpo + " " + consulta;
        var numerosDelManual = new List<string>();
        foreach (Match numero in PatronNumero.Matches(textoDelManual))
        {
            numerosDelManual.Add(numero.Value.Replace(',', '.'));
        }

        foreach (Match numero in PatronNumero.Matches(titulo + " " + resumen))
        {
            var numeroDelResumen = numero.Value.Replace(',', '.');
            if (!numerosDelManual.Contains(numeroDelResumen))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Arma el texto que ve el técnico. Con título de la IA, la primera línea es ese título en español; sin título
    /// (texto del manual sin resumir) se muestra el contexto previo tal como está en el manual. El título original
    /// de la sección queda en la fuente, para encontrarla en el PDF.
    /// </summary>
    public static string ArmarRespuesta(string resumen, List<string> advertencias,
        SeccionDocumento elegida, List<SeccionDocumento> opciones, string titulo = "")
    {
        var respuesta = "";
        if (titulo.Length > 0)
        {
            respuesta += titulo + "\n";
        }
        else if (elegida.Contexto.Length > 0)
        {
            respuesta += elegida.Contexto + "\n";
        }

        respuesta += resumen + "\n";

        if (advertencias.Count > 0)
        {
            respuesta += "\nAdvertencias:\n";
            foreach (var advertencia in advertencias)
            {
                respuesta += "- " + advertencia + "\n";
            }
        }

        if (opciones.Count > 1)
        {
            respuesta += "\nAclaración:\n";
            respuesta += "La consulta puede referirse a más de un procedimiento del manual. ";
            respuesta += "Esta respuesta corresponde a \"" + TextoDelManual.CrearTituloLegible(elegida.Titulo) + "\". ";
            respuesta += "También se encontraron:\n";
            foreach (var opcion in opciones)
            {
                if (opcion.Titulo == elegida.Titulo)
                {
                    continue;
                }

                respuesta += "- " + TextoDelManual.CrearTituloLegible(opcion.Titulo) + " (página " + opcion.Pagina + ")\n";
            }

            respuesta += "Indique cuál necesita para obtener una respuesta precisa.\n";
        }

        respuesta += "\nFuente:\n" + elegida.Documento + ", página " + elegida.Pagina;
        if (elegida.Titulo.Length > 0)
        {
            respuesta += ", sección \"" + TextoDelManual.CrearTituloLegible(elegida.Titulo) + "\"";
        }

        respuesta += ".";
        return respuesta;
    }
}

public record RespuestaDelModelo(string Titulo, string Resumen);
