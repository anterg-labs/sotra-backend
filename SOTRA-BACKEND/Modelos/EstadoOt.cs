using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class EstadoOt
{
    public long IdEstadoOt { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Estado { get; set; }

    public virtual ICollection<HistorialEstadoOt> HistorialEstadoOts { get; set; } = new List<HistorialEstadoOt>();
}
