using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.ModelosMaquina;

public class ModeloMaquinaActualizarDto
{
    [Required, MaxLength(60)]
    public required string Nombre { get; init; }

    [Range(1, long.MaxValue)]
    public required long TipoMaquinaId { get; init; }

    public string? Descripcion { get; init; }

    public string? Estado { get; init; }
}
