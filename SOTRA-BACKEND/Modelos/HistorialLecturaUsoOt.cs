namespace SOTRA_BACKEND.Modelos;

/// <summary>Cada cambio de la lectura de uso de una orden, con quién y cuándo.</summary>
public partial class HistorialLecturaUsoOt
{
    public long IdHLecturaUsoOt { get; set; }

    public long IdOrden { get; set; }

    public decimal? LecturaAnterior { get; set; }

    public decimal? LecturaNueva { get; set; }

    public long? IdUsuario { get; set; }

    public DateTime FechaHora { get; set; }

    public virtual OrdenTrabajo IdOrdenNavigation { get; set; } = null!;

    public virtual Usuario? IdUsuarioNavigation { get; set; }
}
