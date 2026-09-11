using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class OtRepuesto
{
    public long IdOtRepuesto { get; set; }

    public long IdOrden { get; set; }

    public long IdRepuesto { get; set; }

    public virtual OrdenTrabajo IdOrdenNavigation { get; set; } = null!;

    public virtual Repuesto IdRepuestoNavigation { get; set; } = null!;

    public override string ToString()
    {
        return "OtRepuesto: ID: " + IdOtRepuesto + " - ID_ORDEN: " + IdOrden + " - ID_REPUESTO: " + IdRepuesto;
    }
}
