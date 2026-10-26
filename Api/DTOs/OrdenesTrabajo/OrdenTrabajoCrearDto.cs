using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.OrdenesTrabajo;

public class OrdenTrabajoCrearDto
{
    public const int LargoMaximoDeTexto = 4000;

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
    [MaxLength(LargoMaximoDeTexto, ErrorMessage = "La descripción no puede superar los 4000 caracteres.")]
    public required string Descripcion { get; init; }

    [MaxLength(LargoMaximoDeTexto, ErrorMessage = "Las observaciones no pueden superar los 4000 caracteres.")]
    public string? Observaciones { get; init; } = "";

    public DateOnly FechaPrevistaEjecucion { get; init; }

    [Required]
    public List<string> Sugerencias { get; init; } = new();

    [Required]
    public List<long> RepuestosIds { get; init; } = new();

    // El alta ignora el estado recibido: la orden nace en el estado inicial del catálogo.
    public string? Estado { get; init; }
    public Guid? Sesion { get; init; }
}
