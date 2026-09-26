using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class HistorialRecomendacionIa
{
    public long IdRecomendacionIa { get; set; }

    public string Recomendacion { get; set; } = null!;

    public long IdOrden { get; set; }

    public bool Estado { get; set; }

    public virtual OrdenTrabajo IdOrdenNavigation { get; set; } = null!;

    public virtual ICollection<OrdenTrabajo> OrdenTrabajos { get; set; } = new List<OrdenTrabajo>();
}
