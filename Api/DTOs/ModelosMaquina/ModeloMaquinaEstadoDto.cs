using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.ModelosMaquina;

public class ModeloMaquinaEstadoDto
{
    [Required]
    public required string Estado { get; init; }
}
