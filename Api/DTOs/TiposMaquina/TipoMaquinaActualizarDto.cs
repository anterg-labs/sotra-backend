using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.TiposMaquina;

public class TipoMaquinaActualizarDto
{
    [Required, MaxLength(60)]
    public required string Nombre { get; init; }

    public string? Estado { get; init; }
}
