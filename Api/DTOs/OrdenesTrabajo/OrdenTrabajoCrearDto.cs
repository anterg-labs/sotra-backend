using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.OrdenesTrabajo;

public class OrdenTrabajoCrearDto
{
    public const int LargoMaximoDeTexto = 4000;

    /// <summary>Identificador del cliente.</summary>
    [Range(1, long.MaxValue)]
    public long ClienteId { get; init; }

    /// <summary>Identificador del activo.</summary>
    [Range(1, long.MaxValue)]
    public long ActivoId { get; init; }

    /// <summary>Identificador del técnico.</summary>
    public long? TecnicoId { get; init; }

    /// <summary>Tipo; los valores posibles dependen del recurso (por ejemplo `preventivo` o `correctivo`).</summary>
    [Required]
    public required string Tipo { get; init; }

    /// <summary>Prioridad: `alta`, `media` o `baja`.</summary>
    [Required]
    public required string Prioridad { get; init; }

    /// <summary>Descripción.</summary>
    [Required]
    [MaxLength(LargoMaximoDeTexto, ErrorMessage = "La descripción no puede superar los 4000 caracteres.")]
    public required string Descripcion { get; init; }

    /// <summary>Observaciones adicionales, opcionales.</summary>
    [MaxLength(LargoMaximoDeTexto, ErrorMessage = "Las observaciones no pueden superar los 4000 caracteres.")]
    public string? Observaciones { get; init; } = "";

    /// <summary>Fecha prevista de ejecución (`aaaa-mm-dd`).</summary>
    public DateOnly FechaPrevistaEjecucion { get; init; }

    /// <summary>Sugerencias de IA aceptadas para la orden.</summary>
    [Required]
    public List<string> Sugerencias { get; init; } = new();

    /// <summary>Repuestos con sus cantidades.</summary>
    [Required]
    public List<RepuestoPedidoDto> Repuestos { get; init; } = new();

    // El alta ignora el estado recibido: la orden nace en el estado inicial del catálogo.
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public string? Estado { get; init; }
    /// <summary>Identificador de la sesión de sugerencias que originó la orden.</summary>
    public Guid? Sesion { get; init; }
}
