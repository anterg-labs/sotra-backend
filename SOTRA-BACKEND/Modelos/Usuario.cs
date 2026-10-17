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

    public bool Estado { get; set; }

    public DateOnly FechaIngreso { get; set; }

    public Guid? IdKeycloak { get; set; }

    public virtual ICollection<MovimientoStock> MovimientoStocks { get; set; } = new List<MovimientoStock>();

    public virtual ICollection<OrdenTrabajo> OrdenTrabajoIdUsuarioResponsableNavigations { get; set; } = new List<OrdenTrabajo>();

    public virtual ICollection<OrdenTrabajo> OrdenTrabajoIdUsuarioTecnicoNavigations { get; set; } = new List<OrdenTrabajo>();

    public virtual ICollection<Rol> IdRols { get; set; } = new List<Rol>();

    public override string ToString()
    {
        return $"Id: {IdUsuario}, Nombre: {Nombre}, Apellido: {Apellido}";
    }
}
