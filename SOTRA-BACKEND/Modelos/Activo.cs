using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Activo
{
    public long IdActivo { get; set; }

    public long IdCliente { get; set; }

    public string NumSerie { get; set; } = null!;

    public string? Descripcion { get; set; }

    public long IdModeloMaquina { get; set; }

    public short? AnioFabricacion { get; set; }

    public string? Nombre { get; set; }

    public string? Ubicacion { get; set; }

    public bool Activo1 { get; set; }

    public virtual Cliente IdClienteNavigation { get; set; } = null!;

    public virtual ModeloMaquina IdModeloMaquinaNavigation { get; set; } = null!;

    public virtual ICollection<OrdenTrabajo> OrdenTrabajos { get; set; } = new List<OrdenTrabajo>();
}
