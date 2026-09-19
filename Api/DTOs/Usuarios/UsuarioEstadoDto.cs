using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Usuarios;

public class UsuarioEstadoDto
{
    [Required]
    public required string Estado { get; init; }
}
