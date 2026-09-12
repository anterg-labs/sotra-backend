using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Permiso
{
    public long IdPermiso { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public virtual ICollection<Rol> IdRols { get; set; } = new List<Rol>();
}
