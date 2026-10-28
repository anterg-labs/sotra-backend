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

    /// <summary>Tiempo estimado en horas, opcional. Si no se envía, no cambia lo guardado.</summary>
    [Range(0.01, 10000.0, ErrorMessage = "El tiempo estimado debe estar entre 0,01 y 10000 horas.")]
    public decimal? TiempoEstimadoHoras { get; init; }

    /// <summary>Causa de falla del catálogo, opcional y solo para correctivas. Si no se envía, no cambia lo guardado.</summary>
    [Range(1, long.MaxValue)]
    public long? CausaFallaId { get; init; }

    /// <summary>Lectura del horómetro o del odómetro del activo, opcional. Si no se envía, no cambia lo guardado.</summary>
    [Range(0.0, 99999999999.0, ErrorMessage = "La lectura de uso no puede ser negativa.")]
    public decimal? LecturaUso { get; init; }

    /// <summary>Código del motivo de pausa o devolución del catálogo, opcional (`falta_repuesto`, `esperando_autorizacion`, `esperando_cliente` u `otro`).</summary>
    [MaxLength(30)]
    public string? MotivoCodigo { get; init; }

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
