using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Rol
{
    public long IdRol { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    public virtual ICollection<Permiso> IdPermisos { get; set; } = new List<Permiso>();

    public virtual ICollection<Usuario> IdUsuarios { get; set; } = new List<Usuario>();
}
