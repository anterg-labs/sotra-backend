using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.OrdenesTrabajo;

public class OrdenTrabajoCrearDto
{
    [Range(1, long.MaxValue)]
    public long ClienteId { get; init; }

    [Range(1, long.MaxValue)]
    public long ActivoId { get; init; }

    public long? TecnicoId { get; init; }

    [Required]
    public required string Tipo { get; init; }

    [Required]
    public required string Prioridad { get; init; }

    [Required]
    public required string Descripcion { get; init; }

    public string Observaciones { get; init; } = "";

    public DateOnly FechaPrevistaEjecucion { get; init; }

    public List<string> Sugerencias { get; init; } = new();

    public List<long> RepuestosIds { get; init; } = new();

    public string Estado { get; init; } = "creada";
}
