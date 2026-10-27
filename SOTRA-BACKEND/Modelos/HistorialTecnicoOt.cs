namespace SOTRA_BACKEND.Modelos;

/// <summary>Cada asignación o cambio de técnico de una orden: de quién a quién, quién lo hizo y cuándo.</summary>
public partial class HistorialTecnicoOt
{
    public long IdHTecnicoOt { get; set; }

    public long IdOrden { get; set; }

    public long? IdTecnicoAnterior { get; set; }

    public long? IdTecnicoNuevo { get; set; }

    public long? IdUsuario { get; set; }

    public DateTime FechaHora { get; set; }

    public string? Observacion { get; set; }

    public virtual OrdenTrabajo IdOrdenNavigation { get; set; } = null!;

    public virtual Usuario? IdTecnicoAnteriorNavigation { get; set; }

    public virtual Usuario? IdTecnicoNuevoNavigation { get; set; }

    public virtual Usuario? IdUsuarioNavigation { get; set; }
}
