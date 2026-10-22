namespace Api.DTOs.Repuestos;

public class FiltrosRepuestosDto
{
    public string? Buscar { get; init; }
    public string? Estado { get; init; }
    public string? Ubicacion { get; init; }
    public bool? BajoStock { get; init; }

    // "bajo" o "sin_stock": solo los repuestos en ese estado de stock.
    public string? EstadoStock { get; init; }

    // "criticidad": primero sin stock, después stock bajo (del más cercano a cero), después el resto.
    public string? Orden { get; init; }
    public int Pagina { get; init; } = 1;
    public int TamanoPagina { get; init; } = PaginaDto<RepuestoDto>.TamanoPaginaPorDefecto;
}
