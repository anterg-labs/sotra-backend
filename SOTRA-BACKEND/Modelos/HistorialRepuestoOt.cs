namespace SOTRA_BACKEND.Modelos;

/// <summary>Cada cambio de un repuesto en una orden (reservar, modificar, entregar, descontar, liberar), con quién y cuándo.</summary>
public partial class HistorialRepuestoOt
{
    public long IdHRepuestoOt { get; set; }

    public long IdOrden { get; set; }

    public long IdRepuesto { get; set; }

    public string Accion { get; set; } = null!;

    public int? CantidadAnterior { get; set; }

    public int CantidadNueva { get; set; }

    public string? EstadoAnterior { get; set; }

    public string EstadoNuevo { get; set; } = null!;

    public long? IdUsuario { get; set; }

    public DateTime FechaHora { get; set; }

    public long? IdMovimiento { get; set; }

    public string? Observacion { get; set; }

    public virtual OrdenTrabajo IdOrdenNavigation { get; set; } = null!;

    public virtual Repuesto IdRepuestoNavigation { get; set; } = null!;

    public virtual Usuario? IdUsuarioNavigation { get; set; }

    public virtual MovimientoStock? IdMovimientoNavigation { get; set; }
}
