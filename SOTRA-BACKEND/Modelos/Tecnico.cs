using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Tecnico
{
    public long IdTecnico { get; set; }

    public long IdUsuario { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;

    public virtual ICollection<OrdenTrabajo> OrdenTrabajos { get; set; } = new List<OrdenTrabajo>();

    public override string ToString()
    {
        return "Tecnico: ID: " + IdTecnico + " - ID_USUARIO: " + IdUsuario;
    }
}
