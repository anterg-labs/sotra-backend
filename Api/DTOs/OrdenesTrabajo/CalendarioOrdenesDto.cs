using System.ComponentModel.DataAnnotations;

namespace Api.DTOs.OrdenesTrabajo;

public class FiltrosCalendarioDto
{
    /// <summary>Primer día del rango (`aaaa-mm-dd`), obligatorio.</summary>
    [Required]
    public DateOnly? Desde { get; init; }

    /// <summary>Último día del rango (`aaaa-mm-dd`), obligatorio; el rango no puede superar los 100 días.</summary>
    [Required]
    public DateOnly? Hasta { get; init; }

    /// <summary>Filtra por el id del cliente.</summary>
    public long? ClienteId { get; init; }

    /// <summary>Filtra por el id del activo.</summary>
    public long? ActivoId { get; init; }

    /// <summary>Filtra por estado; si se informa, se muestran también las órdenes de ese estado aunque sea `eliminado`.</summary>
    public string? Estado { get; init; }

    /// <summary>Filtra por tipo: `preventivo` o `correctivo`.</summary>
    public string? Tipo { get; init; }

    /// <summary>Filtra por prioridad: `alta`, `media` o `baja`.</summary>
    public string? Prioridad { get; init; }

    /// <summary>Si es `true`, incluye las órdenes eliminadas; por defecto no se muestran.</summary>
    public bool IncluirEliminadas { get; init; }
}

/// <summary>Una orden ubicada en el calendario por su fecha prevista de ejecución.</summary>
public class CalendarioOrdenDto
{
    /// <summary>Identificador de la orden.</summary>
    public long Id { get; init; }

    /// <summary>Fecha prevista de ejecución (`aaaa-mm-dd`).</summary>
    public required string Fecha { get; init; }

    /// <summary>Nombre del activo.</summary>
    public required string NombreActivo { get; init; }

    /// <summary>Nombre del cliente.</summary>
    public required string NombreCliente { get; init; }

    /// <summary>Tipo: `preventivo` o `correctivo`.</summary>
    public required string Tipo { get; init; }

    /// <summary>Prioridad: `alta`, `media` o `baja`.</summary>
    public required string Prioridad { get; init; }

    /// <summary>Valor del estado actual del catálogo.</summary>
    public required string Estado { get; init; }

    /// <summary>Fase del estado actual (`pendiente`, `en_curso`, `por_validar`, `cerrada` o `eliminada`).</summary>
    public required string Fase { get; init; }

    /// <summary>`true` si la orden sigue abierta y su fecha prevista ya pasó.</summary>
    public bool Vencida { get; init; }
}

public class CalendarioOrdenesDto
{
    /// <summary>Primer día pedido.</summary>
    public required string Desde { get; init; }

    /// <summary>Último día pedido.</summary>
    public required string Hasta { get; init; }

    /// <summary>`true` si había más órdenes que el máximo de la respuesta.</summary>
    public bool Truncado { get; init; }

    /// <summary>Órdenes del rango, por fecha prevista.</summary>
    public required List<CalendarioOrdenDto> Elementos { get; init; }
}
