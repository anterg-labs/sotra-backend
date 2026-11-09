namespace Api.Servicios;

/// <summary>Ubicación de los archivos de las evidencias dentro del bucket: evidencias/{idOrden}/{guid}.{ext}.</summary>
public static class ClaveDeEvidencia
{
    public const string Prefijo = "evidencias/";

    public static string Crear(long idOrden, string? tipoContenido)
    {
        return $"{Prefijo}{idOrden}/{Guid.NewGuid():N}{Extension(tipoContenido)}";
    }

    private static string Extension(string? tipoContenido)
    {
        return tipoContenido switch
        {
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            "application/pdf" => ".pdf",
            _ => "",
        };
    }
}
