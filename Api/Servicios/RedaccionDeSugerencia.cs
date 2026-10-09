using System.Text.RegularExpressions;

namespace Api.Servicios;

public static class RedaccionDeSugerencia
{
    private const int LargoMaximoDeTextoParaQwen = 3000;
    private const int LargoMinimoDeResumen = 20;
    private const int LargoMaximoDeResumen = 1200;

    private static readonly Regex PatronNumero = new("\\d+(?:[.,]\\d+)?");

    public const string InstruccionesParaQwen =
        "Resumís secciones de manuales técnicos para técnicos de mantenimiento. " +
        "Reglas obligatorias: " +
        "1) Usá únicamente la información del texto del manual que se te entrega. " +
        "2) No agregues pasos, datos, causas ni recomendaciones que no estén en el texto. " +
        "3) Copiá exactamente los números, unidades, intervalos, cantidades, medidas y nombres de componentes. " +
        "Mantené cada condición y su acción tal como las expresa el manual: " +
        "no cambies el verbo de la acción ni conviertas una acción en otra distinta. " +
        "4) Escribí en español neutro, de 2 a 4 oraciones, en prosa, sin títulos ni listas. " +
        "Usá forma impersonal o de usted (por ejemplo: \"se debe\", \"retire\", \"coloque\"), nunca de tú. " +
        "5) Si el texto o el contexto previo indican un intervalo, un momento o una condición para realizar " +
        "la tarea, empezá por eso, con la misma acción que indica el manual. Si no hay intervalo ni condición, " +
        "empezá directamente por la acción principal. No empieces repitiendo el título de la sección. " +
        "6) Si el texto indica capacidades, cantidades o valores de ajuste, incluilos. " +
        "7) No repitas advertencias de seguridad; se muestran aparte. " +
        "8) Si el texto describe pasos, resumí los principales en orden. " +
        "9) No menciones páginas, secciones ni el nombre del manual.";

    public static string CrearMensajeParaQwen(string consulta, SeccionDocumento seccion, string textoParaResumir)
    {
        var textoDelManual = TextoDelManual.QuitarMarcasInternas(textoParaResumir);
        if (textoDelManual.Length > LargoMaximoDeTextoParaQwen)
        {
            textoDelManual = textoDelManual.Substring(0, LargoMaximoDeTextoParaQwen);
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

    public static bool EsResumenConfiable(string resumen, string consulta, SeccionDocumento seccion)
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

        foreach (Match numero in PatronNumero.Matches(resumen))
        {
            var numeroDelResumen = numero.Value.Replace(',', '.');
            if (!numerosDelManual.Contains(numeroDelResumen))
            {
                return false;
            }
        }

        return true;
    }

    public static string ArmarRespuesta(string resumen, List<string> advertencias,
        SeccionDocumento elegida, List<SeccionDocumento> opciones)
    {
        var respuesta = "";
        if (elegida.Contexto.Length > 0)
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
