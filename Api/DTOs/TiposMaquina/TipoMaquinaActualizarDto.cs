using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.TiposMaquina;

public class TipoMaquinaActualizarDto
{
    [MaxLength(60)]
    public string? Nombre { get; init; }
}
