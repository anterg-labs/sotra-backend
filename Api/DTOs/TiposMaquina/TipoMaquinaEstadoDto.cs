using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.TiposMaquina;

public class TipoMaquinaEstadoDto
{
    [Required]
    public required string Estado { get; init; }
}
