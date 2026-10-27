namespace Api.DTOs.Reportes;

public class FiltrosIndicadoresDto
{
    /// <summary>Fecha inicial del rango (inclusive), en formato ISO 8601 (`aaaa-mm-dd`).</summary>
    public DateOnly? FechaDesde { get; init; }
    /// <summary>Fecha final del rango (inclusive), en formato ISO 8601 (`aaaa-mm-dd`).</summary>
    public DateOnly? FechaHasta { get; init; }
    /// <summary>Filtra por el id del cliente.</summary>
    public long? ClienteId { get; init; }
    /// <summary>Filtra por el id del activo.</summary>
    public long? ActivoId { get; init; }
    /// <summary>Filtra por estado.</summary>
    public string? Estado { get; init; }
    /// <summary>Filtra por tipo.</summary>
    public string? Tipo { get; init; }
    /// <summary>Filtra por prioridad: `alta`, `media` o `baja`.</summary>
    public string? Prioridad { get; init; }
}
