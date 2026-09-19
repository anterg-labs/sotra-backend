using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Clientes;

public class ClienteEstadoDto
{
    [Required]
    public required string Estado { get; init; }
}
