using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Activo
{
    public long IdActivo { get; set; }

    public long IdCliente { get; set; }

    public long IdMaquina { get; set; }

    public string NumSerie { get; set; } = null!;

    public string? Descripcion { get; set; }

    public virtual Cliente IdClienteNavigation { get; set; } = null!;

    public virtual Maquina IdMaquinaNavigation { get; set; } = null!;

    public virtual ICollection<OrdenTrabajo> OrdenTrabajos { get; set; } = new List<OrdenTrabajo>();
}
