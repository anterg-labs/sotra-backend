namespace Api.DTOs.Repuestos;

public class FiltrosRepuestosDto
{
    /// <summary>Texto a buscar en los campos principales, sin distinguir mayúsculas ni tildes.</summary>
    public string? Buscar { get; init; }
    /// <summary>Filtra por estado.</summary>
    public string? Estado { get; init; }
    /// <summary>Filtra por ubicación o depósito.</summary>
    public string? Ubicacion { get; init; }
    /// <summary>Filtra por categoría o tipo, sin distinguir mayúsculas ni tildes.</summary>
    public string? Categoria { get; init; }
    /// <summary>Con `true`, solo los repuestos con stock por debajo del mínimo.</summary>
    public bool? BajoStock { get; init; }
    /// <summary>Estado del stock: sin stock, stock bajo o normal.</summary>
    public string? EstadoStock { get; init; }
    /// <summary>Criterio de orden predefinido.</summary>
    public string? Orden { get; init; }
    /// <summary>Columna por la que se ordena (solo las columnas que acepta cada listado).</summary>
    public string? OrdenarPor { get; init; }
    /// <summary>Dirección del orden: `asc` o `desc`.</summary>
    public string? DireccionOrden { get; init; }
    /// <summary>Número de página, desde 1.</summary>
    public int Pagina { get; init; } = 1;
    /// <summary>Cantidad de elementos por página (máximo permitido por la API).</summary>
    public int TamanoPagina { get; init; } = PaginaDto<RepuestoDto>.TamanoPaginaPorDefecto;
}
