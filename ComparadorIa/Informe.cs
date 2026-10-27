using System.Globalization;
using System.Text;
using Api.Servicios;

namespace ComparadorIa;

public class IntentoDeGeneracion
{
    public string Proveedor { get; }
    public bool Exito { get; set; }
    public string? Error { get; set; }
    public double Segundos { get; set; }
    public string Texto { get; set; } = "";
    public long TokensEntrada { get; set; }
    public long TokensSalida { get; set; }

    /// <summary>Resultado de RedaccionDeSugerencia.EsResumenConfiable, el mismo control que aplica la API.</summary>
    public bool Confiable { get; set; }

    public List<string> ValoresFaltantes { get; } = new();

    public IntentoDeGeneracion(string proveedor)
    {
        Proveedor = proveedor;
    }
}

public class ResultadoDelCaso
{
    public CasoDeEvaluacion Caso { get; }
    public SeccionDocumento? Seccion { get; set; }
    public string ResumenSinIa { get; set; } = "";
    public List<IntentoDeGeneracion> Intentos { get; } = new();

    public ResultadoDelCaso(CasoDeEvaluacion caso)
    {
        Caso = caso;
    }
}

public static class Informe
{
    private const int ConsultasParaEstimarCosto = 1000;
    private static readonly CultureInfo Formato = CultureInfo.GetCultureInfo("es-UY");

    public static string Escribir(string carpeta, List<ResultadoDelCaso> resultados,
        List<IProveedorDeTexto> proveedores, int repeticiones)
    {
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "comparacion-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".md");

        var texto = new StringBuilder();
        texto.AppendLine("# Comparación de proveedores de IA");
        texto.AppendLine();
        texto.AppendLine("Fecha: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " · Casos: " + resultados.Count
            + " · Pedidos por caso y proveedor: " + repeticiones);
        texto.AppendLine();
        texto.AppendLine("Todos los proveedores reciben las mismas instrucciones y la misma sección del manual que usa la API. "
            + "\"Confiable\" es el control de números de `EsResumenConfiable`: si falla, la API muestra el resumen sin IA.");
        texto.AppendLine();

        EscribirResumen(texto, resultados, proveedores);
        EscribirDetalle(texto, resultados);

        File.WriteAllText(ruta, texto.ToString());
        return Path.GetFullPath(ruta);
    }

    private static void EscribirResumen(StringBuilder texto, List<ResultadoDelCaso> resultados,
        List<IProveedorDeTexto> proveedores)
    {
        if (proveedores.Count == 0)
        {
            return;
        }

        texto.AppendLine("## Resumen");
        texto.AppendLine();
        texto.AppendLine("| Proveedor | Modelo | Respondió | Confiable | Valores conservados | Mediana (s) | Máximo (s) | Tokens entrada / salida | USD cada " + ConsultasParaEstimarCosto + " consultas |");
        texto.AppendLine("|---|---|---|---|---|---|---|---|---|");

        foreach (var proveedor in proveedores)
        {
            var configuracion = proveedor.Configuracion;
            var intentos = new List<IntentoDeGeneracion>();
            foreach (var resultado in resultados)
            {
                intentos.AddRange(resultado.Intentos.Where(i => i.Proveedor == configuracion.Nombre));
            }

            var exitosos = intentos.Where(i => i.Exito).ToList();
            var confiables = exitosos.Count(i => i.Confiable);

            var valoresEsperados = 0;
            var valoresConservados = 0;
            foreach (var resultado in resultados)
            {
                foreach (var intento in resultado.Intentos.Where(i => i.Proveedor == configuracion.Nombre && i.Exito))
                {
                    valoresEsperados += resultado.Caso.ValoresEsperados.Count;
                    valoresConservados += resultado.Caso.ValoresEsperados.Count - intento.ValoresFaltantes.Count;
                }
            }

            var segundos = exitosos.Select(i => i.Segundos).Order().ToList();
            var entradaPromedio = exitosos.Count == 0 ? 0 : exitosos.Average(i => i.TokensEntrada);
            var salidaPromedio = exitosos.Count == 0 ? 0 : exitosos.Average(i => i.TokensSalida);
            var costo = ((decimal)entradaPromedio * configuracion.PrecioEntradaPorMillon
                + (decimal)salidaPromedio * configuracion.PrecioSalidaPorMillon) / 1_000_000m * ConsultasParaEstimarCosto;

            texto.AppendLine("| " + configuracion.Nombre
                + " | `" + configuracion.Modelo + "`"
                + " | " + Porcentaje(exitosos.Count, intentos.Count)
                + " | " + Porcentaje(confiables, exitosos.Count)
                + " | " + Porcentaje(valoresConservados, valoresEsperados)
                + " | " + (segundos.Count == 0 ? "-" : Mediana(segundos).ToString("0.00", Formato))
                + " | " + (segundos.Count == 0 ? "-" : segundos[^1].ToString("0.00", Formato))
                + " | " + entradaPromedio.ToString("0", Formato) + " / " + salidaPromedio.ToString("0", Formato)
                + " | " + costo.ToString("0.000", Formato) + " |");
        }

        texto.AppendLine();
        texto.AppendLine("El costo se estima con los precios de `proveedores.json` y los tokens que informó cada proveedor; "
            + "en modelos que razonan, los tokens de razonamiento se cobran como salida.");
        texto.AppendLine();
    }

    private static void EscribirDetalle(StringBuilder texto, List<ResultadoDelCaso> resultados)
    {
        texto.AppendLine("## Detalle por caso");
        foreach (var resultado in resultados)
        {
            texto.AppendLine();
            texto.AppendLine("### " + resultado.Caso.Consulta);
            texto.AppendLine();
            if (resultado.Seccion is null)
            {
                texto.AppendLine("Sin sección del corpus que coincida.");
                continue;
            }

            texto.AppendLine("Sección: " + resultado.Seccion.Documento + ", página " + resultado.Seccion.Pagina
                + ", \"" + TextoDelManual.CrearTituloLegible(resultado.Seccion.Titulo) + "\"");
            if (resultado.Caso.ValoresEsperados.Count > 0)
            {
                texto.AppendLine("Valores esperados: " + string.Join(", ", resultado.Caso.ValoresEsperados));
            }

            texto.AppendLine();
            texto.AppendLine("**Sin IA (resumen extractivo):** " + resultado.ResumenSinIa);

            // Se muestra el primer intento de cada proveedor; el resto solo cuenta para tiempos y porcentajes.
            var mostrados = new HashSet<string>();
            foreach (var intento in resultado.Intentos)
            {
                if (!mostrados.Add(intento.Proveedor))
                {
                    continue;
                }

                texto.AppendLine();
                if (!intento.Exito)
                {
                    texto.AppendLine("**" + intento.Proveedor + ":** error: " + intento.Error);
                    continue;
                }

                var estado = intento.Confiable ? "confiable" : "descartado por el control de números";
                if (intento.ValoresFaltantes.Count > 0)
                {
                    estado += "; faltan " + string.Join(", ", intento.ValoresFaltantes);
                }

                texto.AppendLine("**" + intento.Proveedor + "** (" + intento.Segundos.ToString("0.00", Formato) + " s, " + estado + "): "
                    + intento.Texto.Replace("\n", " "));
            }
        }
    }

    private static string Porcentaje(int parte, int total)
    {
        if (total == 0)
        {
            return "-";
        }

        return (100.0 * parte / total).ToString("0", Formato) + " %";
    }

    private static double Mediana(List<double> ordenados)
    {
        var mitad = ordenados.Count / 2;
        return ordenados.Count % 2 == 1 ? ordenados[mitad] : (ordenados[mitad - 1] + ordenados[mitad]) / 2;
    }
}
