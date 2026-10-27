namespace Api.DTOs.DocumentosTecnicos;

public class FiltrosDocumentosTecnicosDto
{
    /// <summary>Texto a buscar en los campos principales, sin distinguir mayúsculas ni tildes.</summary>
    public string? Buscar { get; init; }
    /// <summary>Filtra por el id del activo.</summary>
    public long? ActivoId { get; init; }
    /// <summary>Filtra por el id del modelo de máquina.</summary>
    public long? ModeloMaquinaId { get; init; }
    /// <summary>Filtra por tipo.</summary>
    public string? Tipo { get; init; }
    /// <summary>Con `true`, solo los documentos vigentes; con `false`, solo los no vigentes.</summary>
    public bool? Vigente { get; init; }
    /// <summary>Columna por la que se ordena (solo las columnas que acepta cada listado).</summary>
    public string? OrdenarPor { get; init; }
    /// <summary>Dirección del orden: `asc` o `desc`.</summary>
    public string? DireccionOrden { get; init; }
    /// <summary>Número de página, desde 1.</summary>
    public int Pagina { get; init; } = 1;
    /// <summary>Cantidad de elementos por página (máximo permitido por la API).</summary>
    public int TamanoPagina { get; init; } = PaginaDto<DocumentoTecnicoDto>.TamanoPaginaPorDefecto;
}
