using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class OtRepuesto
{
    public long IdOtRepuesto { get; set; }

    public long IdOrden { get; set; }

    public long IdRepuesto { get; set; }

    /// <summary>Unidades del repuesto que usa la orden.</summary>
    public int Cantidad { get; set; } = 1;

    /// <summary><c>reservado</c>, <c>entregado</c>, <c>descontado</c> o <c>liberado</c> (ver <c>EstadosRepuestoOrden</c>).</summary>
    public string Estado { get; set; } = "reservado";

    /// <summary>Egreso de stock que descontó estas unidades al cerrar la orden.</summary>
    public long? IdMovimiento { get; set; }

    public virtual MovimientoStock? IdMovimientoNavigation { get; set; }

    public virtual OrdenTrabajo IdOrdenNavigation { get; set; } = null!;

    public virtual Repuesto IdRepuestoNavigation { get; set; } = null!;
}
