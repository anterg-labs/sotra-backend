namespace Api.Servicios;

/// <summary>Ubicación de los PDF de los documentos técnicos dentro del bucket: documentacion/{guid}.pdf.</summary>
public static class ClaveDeDocumentoTecnico
{
    public const string Prefijo = "documentacion/";

    public static string Crear()
    {
        return $"{Prefijo}{Guid.NewGuid():N}.pdf";
    }
}
