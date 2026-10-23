namespace Api.DTOs.Usuarios;

public class UsuarioListadoDto
{
    public long Id { get; init; }


    public required string Nombre { get; init; }

    public required string Apellido { get; init; }

    public required string Correo { get; init; }

    public required string Telefono { get; init; }

    public required string Estado { get; init; }

    public DateOnly FechaIngreso { get; init; }
    public int CantidadOrdenesAsignadas { get; init; }

    public List<RolUsuarioDto> Roles { get; init; } = [];
}
