using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class ModeloMaquina
{
    public long IdModeloMaquina { get; set; }

    public string Nombre { get; set; } = null!;

    public virtual ICollection<Maquina> Maquinas { get; set; } = new List<Maquina>();
}
