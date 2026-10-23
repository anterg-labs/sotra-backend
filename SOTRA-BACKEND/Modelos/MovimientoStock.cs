using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class MovimientoStock
{
    public long IdMovimiento { get; set; }

    public DateTime Fecha { get; set; }

    public int Cantidad { get; set; }

    public long IdRepuesto { get; set; }

    public long? IdUsuario { get; set; }

    public string Tipo { get; set; } = null!;

    public string? Motivo { get; set; }

    public string? Ubicacion { get; set; }

    public string? Observaciones { get; set; }

    public int? StockAnterior { get; set; }

    public int? StockPosterior { get; set; }

    public virtual Repuesto IdRepuestoNavigation { get; set; } = null!;

    public virtual Usuario? IdUsuarioNavigation { get; set; }
}
