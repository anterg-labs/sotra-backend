namespace Api.DTOs.MovimientosStock;

public class FiltrosMovimientosStockDto
{
    /// <summary>Texto a buscar en los campos principales, sin distinguir mayúsculas ni tildes.</summary>
    public string? Buscar { get; init; }
    /// <summary>Filtra por el id del repuesto.</summary>
    public long? RepuestoId { get; init; }
    /// <summary>Filtra por tipo.</summary>
    public string? Tipo { get; init; }
    /// <summary>Fecha inicial del rango (inclusive), en formato ISO 8601 (`aaaa-mm-dd`).</summary>
    public DateOnly? FechaDesde { get; init; }
    /// <summary>Fecha final del rango (inclusive), en formato ISO 8601 (`aaaa-mm-dd`).</summary>
    public DateOnly? FechaHasta { get; init; }
    /// <summary>Filtra por ubicación o depósito.</summary>
    public string? Ubicacion { get; init; }
    /// <summary>Filtra por el id del usuario responsable.</summary>
    public long? UsuarioId { get; init; }
    /// <summary>Columna por la que se ordena (solo las columnas que acepta cada listado).</summary>
    public string? OrdenarPor { get; init; }
    /// <summary>Dirección del orden: `asc` o `desc`.</summary>
    public string? DireccionOrden { get; init; }
    /// <summary>Número de página, desde 1.</summary>
    public int Pagina { get; init; } = 1;
    /// <summary>Cantidad de elementos por página (máximo permitido por la API).</summary>
    public int TamanoPagina { get; init; } = PaginaDto<MovimientoStockDto>.TamanoPaginaPorDefecto;
}
