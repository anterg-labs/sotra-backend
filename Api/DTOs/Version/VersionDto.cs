namespace Api.DTOs.Version;

public class VersionDto
{
    /// <summary>Versión del registro, para el control de cambios concurrentes.</summary>
    public string Version { get; set; } = string.Empty;
}
