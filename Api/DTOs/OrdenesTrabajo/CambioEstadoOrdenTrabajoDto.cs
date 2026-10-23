using System.ComponentModel.DataAnnotations;
namespace Api.DTOs.OrdenesTrabajo;

public class CambioEstadoOrdenTrabajoDto
{
    [Required]
    public required string Estado { get; init; }
    public string? Motivo { get; init; }
}
