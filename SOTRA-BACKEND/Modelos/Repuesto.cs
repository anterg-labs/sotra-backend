using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Repuesto
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public int Cantidad { get; set; }

    public bool Estado { get; set; }

    public string? Ubicacion { get; set; }

    public string? Marca { get; set; }

    public long IdRepuesto { get; set; }

    public int? CantMin { get; set; }

    public virtual ICollection<MovimientoStock> MovimientoStocks { get; set; } = new List<MovimientoStock>();

    public virtual ICollection<OtRepuesto> OtRepuestos { get; set; } = new List<OtRepuesto>();

    public override string ToString()
    {
        return "Repuesto: ID: " + IdRepuesto + " - CODIGO: " + Codigo + " - NOMBRE: " + Nombre + " - DESCRIPCION: " + Descripcion + " - CANTIDAD: " + Cantidad + " - ESTADO: " + Estado + " - UBICACION: " + Ubicacion + " - MARCA: " + Marca + " - CANT_MIN: " + CantMin;
    }
}
