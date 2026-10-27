using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Api.DTOs.OrdenesTrabajo;

public class OrdenTrabajoActualizarDto
{
    /// <summary>Identificador del cliente.</summary>
    [JsonRequired, Range(1, long.MaxValue)]
    public long ClienteId { get; init; }

    /// <summary>Identificador del activo.</summary>
    [JsonRequired, Range(1, long.MaxValue)]
    public long ActivoId { get; init; }

    /// <summary>Identificador del técnico.</summary>
    [JsonRequired, Range(1, long.MaxValue)]
    public long? TecnicoId { get; init; }

    /// <summary>Tipo; los valores posibles dependen del recurso (por ejemplo `preventivo` o `correctivo`).</summary>
    [Required]
    public required string Tipo { get; init; }

    /// <summary>Prioridad: `alta`, `media` o `baja`.</summary>
    [Required]
    public required string Prioridad { get; init; }

    /// <summary>Descripción.</summary>
    [Required, MaxLength(OrdenTrabajoCrearDto.LargoMaximoDeTexto)]
    public required string Descripcion { get; init; }

    /// <summary>Observaciones adicionales, opcionales.</summary>
    [JsonRequired, MaxLength(OrdenTrabajoCrearDto.LargoMaximoDeTexto)]
    public string? Observaciones { get; init; }

    /// <summary>Fecha prevista de ejecución (`aaaa-mm-dd`).</summary>
    [JsonRequired]
    public DateOnly FechaPrevistaEjecucion { get; init; }

    /// <summary>Sugerencias de IA aceptadas para la orden.</summary>
    [JsonRequired, Required]
    public List<string> Sugerencias { get; init; } = [];

    /// <summary>Repuestos con sus cantidades.</summary>
    [JsonRequired, Required]
    public List<RepuestoPedidoDto> Repuestos { get; init; } = [];

    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    [Required]
    public required string Estado { get; init; }

    /// <summary>Motivo del cambio de estado (obligatorio en las transiciones que lo exigen).</summary>
    public string? MotivoEstado { get; init; }
    /// <summary>Identificador de la sesión de sugerencias que originó la orden.</summary>
    public Guid? Sesion { get; init; }
}
