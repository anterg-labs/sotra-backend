namespace Api.DTOs.Usuarios;

public class RolUsuarioDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }

    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }

    /// <summary>Indica si está activo.</summary>
    public bool Activo { get; init; }
}
