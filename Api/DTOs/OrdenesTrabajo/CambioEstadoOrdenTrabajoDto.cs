using System.ComponentModel.DataAnnotations;
namespace Api.DTOs.OrdenesTrabajo;

public class CambioEstadoOrdenTrabajoDto
{
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    [Required]
    public required string Estado { get; init; }
    /// <summary>Motivo del cambio o movimiento.</summary>
    public string? Motivo { get; init; }
}
