using System.ComponentModel.DataAnnotations;
namespace Api.DTOs.OrdenesTrabajo;

public class CambioEstadoOrdenTrabajoDto
{
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    [Required]
    public required string Estado { get; init; }
    /// <summary>Motivo del cambio o movimiento.</summary>
    public string? Motivo { get; init; }
    /// <summary>Código del motivo del catálogo (`falta_repuesto`, `esperando_autorizacion`, `esperando_cliente` u `otro`), opcional.</summary>
    [MaxLength(30)]
    public string? MotivoCodigo { get; init; }
    /// <summary>Lectura del horómetro o del odómetro del activo, opcional.</summary>
    [Range(0.0, 99999999999.0, ErrorMessage = "La lectura de uso no puede ser negativa.")]
    public decimal? LecturaUso { get; init; }
}
