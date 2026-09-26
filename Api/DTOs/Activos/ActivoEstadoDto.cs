using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Activos;

public class ActivoEstadoDto
{
    [Required]
    public required string Estado { get; init; }
}
