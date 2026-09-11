using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Gerente
{
    public long IdGerente { get; set; }

    public long IdUsuario { get; set; }

    public virtual Usuario IdUsuarioNavigation { get; set; } = null!;
}
