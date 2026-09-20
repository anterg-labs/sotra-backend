using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.ModelosMaquina;

public class ModeloMaquinaActualizarDto
{
    [MaxLength(60)]
    public string? Nombre { get; init; }

    [Range(1, long.MaxValue)]
    public long? TipoMaquinaId { get; init; }

    public string? Descripcion { get; init; }
}
