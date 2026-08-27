using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class Maquina
{
    public long IdMaquina { get; set; }

    public string Marca { get; set; } = null!;

    public long IdModeloMaquina { get; set; }

    public short Anio { get; set; }

    public long IdTipoMaquina { get; set; }

    public string? Descripcion { get; set; }

    public virtual ICollection<Activo> Activos { get; set; } = new List<Activo>();

    public virtual ModeloMaquina IdModeloMaquinaNavigation { get; set; } = null!;

    public virtual TipoMaquina IdTipoMaquinaNavigation { get; set; } = null!;
}
