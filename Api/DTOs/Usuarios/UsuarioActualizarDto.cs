using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Usuarios;

public class UsuarioActualizarDto
{
    [MaxLength(100)]
    public string? Nombre { get; init; }

    [MaxLength(100)]
    public string? Apellido { get; init; }

    [EmailAddress, MaxLength(254)]
    public string? Correo { get; init; }

    [MaxLength(30)]
    public string? Telefono { get; init; }

    public string? Contrasena { get; init; }

    public string? Estado { get; init; }

    public DateOnly? FechaIngreso { get; init; }

    public List<long>? IdsRol { get; init; }
}
