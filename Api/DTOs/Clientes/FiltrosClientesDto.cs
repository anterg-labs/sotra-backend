namespace Api.DTOs.Clientes;

public class FiltrosClientesDto
{
    /// <summary>Texto a buscar en los campos principales, sin distinguir mayúsculas ni tildes.</summary>
    public string? Buscar { get; init; }
    /// <summary>Filtra por estado.</summary>
    public string? Estado { get; init; }
    /// <summary>Con `true`, solo los clientes con al menos un activo asociado.</summary>
    public bool ConActivos { get; init; }
    /// <summary>Columna por la que se ordena (solo las columnas que acepta cada listado).</summary>
    public string? OrdenarPor { get; init; }
    /// <summary>Dirección del orden: `asc` o `desc`.</summary>
    public string? DireccionOrden { get; init; }
    /// <summary>Número de página, desde 1.</summary>
    public int Pagina { get; init; } = 1;
    /// <summary>Cantidad de elementos por página (máximo permitido por la API).</summary>
    public int TamanoPagina { get; init; } = PaginaDto<ClienteListadoDto>.TamanoPaginaPorDefecto;
}
