namespace SOTRA_BACKEND.Operaciones;

internal static class ConsultasDeTexto
{
    /// <summary>Arma un patrón ILIKE de "contiene", escapando los comodines que escriba la persona.</summary>
    public static string PatronContiene(string texto)
    {
        var escapado = texto.Trim()
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
        return $"%{escapado}%";
    }
}
