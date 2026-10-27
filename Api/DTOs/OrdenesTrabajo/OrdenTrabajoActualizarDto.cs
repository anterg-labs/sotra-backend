using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.DTOs.OrdenesTrabajo;

public class OrdenTrabajoActualizarDto
{
    [JsonRequired, Range(1, long.MaxValue)]
    public long ClienteId { get; init; }

    [JsonRequired, Range(1, long.MaxValue)]
    public long ActivoId { get; init; }

    [JsonRequired, Range(1, long.MaxValue)]
    public long? TecnicoId { get; init; }

    [Required]
    public required string Tipo { get; init; }

    [Required]
    public required string Prioridad { get; init; }

    [Required, MaxLength(OrdenTrabajoCrearDto.LargoMaximoDeTexto)]
    public required string Descripcion { get; init; }

    [JsonRequired, MaxLength(OrdenTrabajoCrearDto.LargoMaximoDeTexto)]
    public string? Observaciones { get; init; }

    [JsonRequired]
    public DateOnly FechaPrevistaEjecucion { get; init; }

    [JsonRequired, Required]
    public List<string> Sugerencias { get; init; } = [];

    [JsonRequired, Required]
    public List<RepuestoPedidoDto> Repuestos { get; init; } = [];

    [Required]
    public required string Estado { get; init; }

    public string? MotivoEstado { get; init; }
    public Guid? Sesion { get; init; }
}
