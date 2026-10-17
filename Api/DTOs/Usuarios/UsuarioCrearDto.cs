using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Usuarios;

public class UsuarioCrearDto
{
    [Required, MaxLength(100)]
    public required string Nombre { get; init; }

    [Required, MaxLength(100)]
    public required string Apellido { get; init; }

    [Required, EmailAddress, MaxLength(254)]
    public required string Correo { get; init; }

    [Required, MaxLength(30)]
    public required string Telefono { get; init; }

    public List<long> IdsRol { get; init; } = [];
}
