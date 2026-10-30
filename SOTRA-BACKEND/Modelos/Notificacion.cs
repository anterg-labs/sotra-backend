namespace SOTRA_BACKEND.Modelos;

/// <summary>Valores cerrados de <c>notificacion.tipo</c>.</summary>
public static class TiposDeNotificacion
{
    public const string OrdenAsignada = "orden_asignada";
    public const string OrdenCambioEstado = "orden_cambio_estado";
    public const string OrdenEnRevision = "orden_en_revision";
}

public partial class Notificacion
{
    public long IdNotificacion { get; set; }

    public long IdUsuario { get; set; }

    public long? IdOrden { get; set; }

    public string Tipo { get; set; } = null!;

    public string Mensaje { get; set; } = null!;

    public DateTime CreadaEn { get; set; }

    public DateTime? LeidaEn { get; set; }
}
