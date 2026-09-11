using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class MovimientoStock
{
    public long IdMovimiento { get; set; }

    public long IdPanolero { get; set; }

    public string Estado { get; set; } = null!;

    public DateTime Fecha { get; set; }

    public decimal Cantidad { get; set; }

    public long IdRepuesto { get; set; }

    public virtual Panolero IdPanoleroNavigation { get; set; } = null!;

    public virtual Repuesto IdRepuestoNavigation { get; set; } = null!;
}
