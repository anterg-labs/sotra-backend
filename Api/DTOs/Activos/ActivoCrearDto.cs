using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.Activos;

public class ActivoCrearDto
{
    [Range(1, long.MaxValue)]
    public long ClienteId { get; init; }

    [Required, MaxLength(100)]
    public required string NumSerie { get; init; }

    public string? Descripcion { get; init; }

    [Range(1, long.MaxValue)]
    public long ModeloMaquinaId { get; init; }

    public short? AnioFabricacion { get; init; }

    [MaxLength(100)]
    public string? Nombre { get; init; }

    [MaxLength(150)]
    public string? Ubicacion { get; init; }
}
