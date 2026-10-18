using System.Text;

namespace Api.Servicios;

public static class GeneradorReportesCsv
{
    private const char Separador = ';';
    private static readonly char[] CaracteresQueExigenComillas = [Separador, '"', '\r', '\n'];
    private static readonly char[] InicioDeFormula = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>Genera un CSV UTF-8 con BOM (para que Excel lea las tildes), una sección por tabla.</summary>
    public static byte[] Generar(IReadOnlyList<TablaReporte> tablas)
    {
        var texto = new StringBuilder();
        for (var indice = 0; indice < tablas.Count; indice++)
        {
            var tabla = tablas[indice];
            if (indice > 0)
            {
                texto.Append("\r\n");
            }

            if (tablas.Count > 1)
            {
                texto.Append(Escapar(tabla.Titulo)).Append("\r\n");
            }

            texto.Append(string.Join(Separador, tabla.Columnas.Select(Escapar))).Append("\r\n");
            foreach (var fila in tabla.Filas)
            {
                texto.Append(string.Join(Separador, fila.Select(Escapar))).Append("\r\n");
            }
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(texto.ToString())).ToArray();
    }

    // Neutraliza las fórmulas (inyección de CSV) y aplica las comillas de RFC 4180.
    public static string Escapar(string valor)
    {
        if (valor.Length > 0 && valor.IndexOfAny(InicioDeFormula) == 0)
        {
            valor = "'" + valor;
        }

        var requiereComillas = valor.IndexOfAny(CaracteresQueExigenComillas) >= 0;
        return requiereComillas ? "\"" + valor.Replace("\"", "\"\"") + "\"" : valor;
    }
}
