namespace Api.DTOs.Notificaciones;

public class NotificacionDto
{
    /// <summary>Identificador de la notificación.</summary>
    public long Id { get; init; }
    /// <summary>Qué la originó: `orden_asignada`, `orden_cambio_estado` u `orden_en_revision`.</summary>
    public required string Tipo { get; init; }
    /// <summary>Texto para mostrar.</summary>
    public required string Mensaje { get; init; }
    /// <summary>Orden de trabajo a la que se refiere, si corresponde.</summary>
    public long? OrdenId { get; init; }
    /// <summary>Instante en que se generó (UTC).</summary>
    public DateTime CreadaEn { get; init; }
    /// <summary>`true` si la persona ya la leyó.</summary>
    public bool Leida { get; init; }
}

public class NoLeidasDto
{
    /// <summary>Cantidad de notificaciones sin leer de la persona con sesión.</summary>
    public int Total { get; init; }
}

public class FiltrosNotificacionesDto
{
    /// <summary>Con `true`, solo las que todavía no se leyeron.</summary>
    public bool SoloNoLeidas { get; init; }
    /// <summary>Número de página, desde 1.</summary>
    public int Pagina { get; init; } = 1;
    /// <summary>Cantidad de elementos por página (máximo permitido por la API).</summary>
    public int TamanoPagina { get; init; } = PaginaDto<NotificacionDto>.TamanoPaginaPorDefecto;
}
