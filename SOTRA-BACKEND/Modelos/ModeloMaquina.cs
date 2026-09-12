using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class ModeloMaquina
{
    public long IdModeloMaquina { get; set; }

    public string Nombre { get; set; } = null!;

    public long IdTipoMaquina { get; set; }

    public string? Descripcion { get; set; }

    public bool Activo { get; set; }

    public virtual ICollection<Activo> Activos { get; set; } = new List<Activo>();

    public virtual TipoMaquina IdTipoMaquinaNavigation { get; set; } = null!;
}
