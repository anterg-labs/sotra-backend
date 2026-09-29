namespace Api.DTOs.Usuarios;

public class RolUsuarioDto
{
    public long Id { get; init; }

    public required string Nombre { get; init; }

    public bool Activo { get; init; }
}
