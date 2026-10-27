namespace Api.DTOs.Sesion;

public class SesionDto
{
    /// <summary>Identificador de la persona en Keycloak.</summary>
    public required string IdKeycloak { get; init; }
    /// <summary>Identificador en SOTRA, o null si no está registrada.</summary>
    public long? IdUsuario { get; init; }
    /// <summary>Correo electrónico.</summary>
    public string? Correo { get; init; }
    /// <summary>Nombre.</summary>
    public string? Nombre { get; init; }
    /// <summary>Roles asignados.</summary>
    public required List<string> Roles { get; init; }
}
