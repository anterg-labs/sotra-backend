using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Panolero
{
    public long IdPanolero { get; set; }

    public long IdUsuario { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;

    public virtual ICollection<MovimientoStock> MovimientoStocks { get; set; } = new List<MovimientoStock>();

    public override string ToString()
    {
        return "Panolero: ID: " + IdPanolero + " - ID_USUARIO: " + IdUsuario;
    }
}
