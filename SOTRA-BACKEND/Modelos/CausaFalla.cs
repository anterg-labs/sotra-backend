namespace SOTRA_BACKEND.Modelos;

/// <summary>Catálogo de causas de falla que se pueden indicar al cerrar una orden correctiva.</summary>
public partial class CausaFalla
{
    public long IdCausaFalla { get; set; }

    public string Nombre { get; set; } = null!;

    public bool Estado { get; set; }

    public virtual ICollection<OrdenTrabajo> OrdenTrabajos { get; set; } = new List<OrdenTrabajo>();
}
