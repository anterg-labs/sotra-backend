using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class HistorialEstadoOt
{
    public long IdHEstadoOt { get; set; }

    public long IdOrden { get; set; }

    public long? IdMotivoEs { get; set; }

    public long IdEstadoOt { get; set; }

    public DateTime FechaHora { get; set; }

    public string? Observacion { get; set; }

    public virtual EstadoOt IdEstadoOtNavigation { get; set; } = null!;

    public virtual MotivoEstadoOt? IdMotivoEsNavigation { get; set; }

    public virtual OrdenTrabajo IdOrdenNavigation { get; set; } = null!;
}
