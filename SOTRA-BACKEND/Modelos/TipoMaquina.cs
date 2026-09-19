using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class TipoMaquina
{
    public long IdTipoMaquina { get; set; }

    public string Nombre { get; set; } = null!;

    public bool Estado { get; set; }

    public virtual ICollection<ModeloMaquina> ModeloMaquinas { get; set; } = new List<ModeloMaquina>();
}
