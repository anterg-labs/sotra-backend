using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Activos;

public class ActivoActualizarDto
{
    [Range(1, long.MaxValue)]
    public long? ClienteId { get; init; }

    [MaxLength(100)]
    public string? NumSerie { get; init; }

    public string? Descripcion { get; init; }

    [Range(1, long.MaxValue)]
    public long? ModeloMaquinaId { get; init; }

    public short? AnioFabricacion { get; init; }

    [MaxLength(100)]
    public string? Nombre { get; init; }

    [MaxLength(150)]
    public string? Ubicacion { get; init; }
}
