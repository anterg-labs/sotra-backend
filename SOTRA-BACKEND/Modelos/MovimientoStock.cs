using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class MovimientoStock
{
    public long IdMovimiento { get; set; }

    public bool Estado { get; set; }

    public DateTime Fecha { get; set; }

    public decimal Cantidad { get; set; }

    public long IdRepuesto { get; set; }

    public long IdUsuario { get; set; }

    public virtual Repuesto IdRepuestoNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
