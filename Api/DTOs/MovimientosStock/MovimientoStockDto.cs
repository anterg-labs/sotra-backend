namespace Api.DTOs.MovimientosStock;

public class MovimientoStockDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Identificador del repuesto.</summary>
    public long RepuestoId { get; init; }
    /// <summary>Código del repuesto.</summary>
    public required string CodigoRepuesto { get; init; }
    /// <summary>Nombre del repuesto.</summary>
    public required string NombreRepuesto { get; init; }
    /// <summary>Tipo; los valores posibles dependen del recurso (por ejemplo `preventivo` o `correctivo`).</summary>
    public required string Tipo { get; init; }
    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    public int Cantidad { get; init; }
    /// <summary>Fecha y hora (ISO 8601, UTC).</summary>
    public DateTime Fecha { get; init; }
    /// <summary>Ubicación o depósito.</summary>
    public string Ubicacion { get; init; } = "";
    /// <summary>Identificador del usuario.</summary>
    public long? UsuarioId { get; init; }
    /// <summary>Nombre de la persona usuaria.</summary>
    public string? NombreUsuario { get; init; }
    /// <summary>Motivo del cambio o movimiento.</summary>
    public string Motivo { get; init; } = "";
    /// <summary>Observaciones adicionales, opcionales.</summary>
    public string Observaciones { get; init; } = "";
    /// <summary>Stock antes del movimiento.</summary>
    public int? StockAnterior { get; init; }
    /// <summary>Stock después del movimiento.</summary>
    public int? StockPosterior { get; init; }
}
