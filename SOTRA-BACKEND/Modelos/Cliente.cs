using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Cliente
{
    public long IdCliente { get; set; }

    public string RazonSocial { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public string? Correo { get; set; }

    public string? Telefono { get; set; }

    public string? Direccion { get; set; }

    public string? Observaciones { get; set; }

    public bool Estado { get; set; }

    public virtual ICollection<Activo> Activos { get; set; } = new List<Activo>();

    public virtual ICollection<OrdenTrabajo> OrdenTrabajos { get; set; } = new List<OrdenTrabajo>();
}
