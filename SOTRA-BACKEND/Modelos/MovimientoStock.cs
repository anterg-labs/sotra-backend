using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class MovimientoStock
{
    public long IdMovimiento { get; set; }

    public long IdPanolero { get; set; }

    public bool Estado { get; set; }

    public DateTime Fecha { get; set; }

    public decimal Cantidad { get; set; }

    public long IdRepuesto { get; set; }

    public virtual Panolero IdPanoleroNavigation { get; set; } = null!;

    public virtual Repuesto IdRepuestoNavigation { get; set; } = null!;

    public override string ToString()
    {
        return "MovimientoStock: ID: " + IdMovimiento + " - ID_PANOLO: " + IdPanolero + " - ESTADO: " + Estado + " - FECHA: " + Fecha + " - CANTIDAD: " + Cantidad + " - ID_REPUESTO: " + IdRepuesto;
    }
}
