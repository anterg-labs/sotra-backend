namespace Api.DTOs.Usuarios;

/// <summary>
/// Resumen de un rol asignado al usuario, sin sus permisos ni usuarios asociados.
/// </summary>
public class RolUsuarioDto
{
    public long Id { get; init; }

    public required string Nombre { get; init; }

    public bool Activo { get; init; }
}
