namespace Api.DTOs.OrdenesTrabajo;

public class FiltrosOrdenesTrabajoDto
{
    /// <summary>Texto a buscar en los campos principales, sin distinguir mayúsculas ni tildes.</summary>
    public string? Buscar { get; init; }
    /// <summary>Filtra por el id del cliente.</summary>
    public long? ClienteId { get; init; }
    /// <summary>Filtra por el id del activo.</summary>
    public long? ActivoId { get; init; }
    /// <summary>Filtra por el id del técnico asignado.</summary>
    public long? TecnicoId { get; init; }
    /// <summary>Filtra por estado.</summary>
    public string? Estado { get; init; }
    /// <summary>Fase del estado (`estado_ot.fase`): agrupa varios estados, por ejemplo `en_curso`.</summary>
    public string? Fase { get; init; }
    /// <summary>`true` deja solo las órdenes activas (pendientes, en curso o por validar): las mismas que cuentan los listados de clientes y usuarios.</summary>
    public bool? SoloActivas { get; init; }
    /// <summary>Filtra por prioridad: `alta`, `media` o `baja`.</summary>
    public string? Prioridad { get; init; }
    /// <summary>Filtra por tipo.</summary>
    public string? Tipo { get; init; }
    /// <summary>Fecha inicial del rango (inclusive), en formato ISO 8601 (`aaaa-mm-dd`).</summary>
    public DateOnly? FechaDesde { get; init; }
    /// <summary>Fecha final del rango (inclusive), en formato ISO 8601 (`aaaa-mm-dd`).</summary>
    public DateOnly? FechaHasta { get; init; }
    /// <summary>Con `true`, solo las órdenes que tienen recomendaciones de IA.</summary>
    public bool? ConSugerencias { get; init; }
    /// <summary>Columna por la que se ordena (solo las columnas que acepta cada listado).</summary>
    public string? OrdenarPor { get; init; }
    /// <summary>Dirección del orden: `asc` o `desc`.</summary>
    public string? DireccionOrden { get; init; }
    /// <summary>Número de página, desde 1.</summary>
    public int Pagina { get; init; } = 1;
    /// <summary>Cantidad de elementos por página (máximo permitido por la API).</summary>
    public int TamanoPagina { get; init; } = PaginaDto<OrdenTrabajoDto>.TamanoPaginaPorDefecto;
}
