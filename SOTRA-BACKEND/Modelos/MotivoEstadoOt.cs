using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class MotivoEstadoOt
{
    public long IdMotivoEs { get; set; }

    public string? Descripcion { get; set; }

    public virtual ICollection<HistorialEstadoOt> HistorialEstadoOts { get; set; } = new List<HistorialEstadoOt>();

    public override string ToString()
    {
        return "MotivoEstadoOt: ID: " + IdMotivoEs + " - DESCRIPCION: " + Descripcion;
    }
}
