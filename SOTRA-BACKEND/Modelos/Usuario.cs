using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Usuario
{
    public long IdUsuario { get; set; }

    public string Nombre { get; set; } = null!;

    public string Apellido { get; set; } = null!;

    public string Mail { get; set; } = null!;

    public string Telefono { get; set; } = null!;

    public string Contrasena { get; set; } = null!;

    public string Estado { get; set; } = null!;

    public DateTime FechaIngreso { get; set; }

    public virtual Administrador? Administrador { get; set; }

    public virtual Gerente? Gerente { get; set; }

    public virtual JefeMantenimiento? JefeMantenimiento { get; set; }

    public virtual Panolero? Panolero { get; set; }

    public virtual Tecnico? Tecnico { get; set; }
}
