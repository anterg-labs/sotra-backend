namespace Api.DTOs.Repuestos;

/// <summary>Una orden que tiene unidades del repuesto reservadas o entregadas (lo que resta del disponible).</summary>
public class OrdenComprometidaDto
{
    /// <summary>Número de la orden de trabajo.</summary>
    public long OrdenId { get; init; }
    /// <summary><c>reservado</c> (la orden todavía no empezó) o <c>entregado</c> (la orden está en curso).</summary>
    public required string EstadoRepuesto { get; init; }
    /// <summary>Unidades del repuesto comprometidas en esa orden.</summary>
    public int Cantidad { get; init; }
    /// <summary>Estado actual de la orden.</summary>
    public required string EstadoOrden { get; init; }
    /// <summary>Cliente de la orden.</summary>
    public required string NombreCliente { get; init; }
    /// <summary>Activo de la orden.</summary>
    public required string NombreActivo { get; init; }
    /// <summary>Técnico asignado; <c>null</c> si la orden no tiene técnico.</summary>
    public string? NombreTecnico { get; init; }
    /// <summary>Fecha prevista de ejecución (<c>yyyy-MM-dd</c>).</summary>
    public DateOnly FechaPrevistaEjecucion { get; init; }
    /// <summary>Desde cuándo las unidades están reservadas o entregadas; <c>null</c> si no quedó registrado.</summary>
    public DateTime? Desde { get; init; }
}
