using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class JefeMantenimiento
{
    public long IdJefeMantenimiento { get; set; }

    public long IdUsuario { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;

    public virtual ICollection<OrdenTrabajo> OrdenTrabajos { get; set; } = new List<OrdenTrabajo>();
}
