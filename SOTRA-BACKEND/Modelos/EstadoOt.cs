using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class EstadoOt
{
    public long IdEstadoOt { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool Estado { get; set; }

    public short Orden { get; set; }

    public string Fase { get; set; } = null!;

    public bool ExigeTecnico { get; set; }

    public string? CompletaFecha { get; set; }

    public string Tono { get; set; } = null!;

    public string? CodigoAlerta { get; set; }

    public bool EsInicial { get; set; }

    public virtual ICollection<TransicionEstadoOt> TransicionesDeOrigen { get; set; } = new List<TransicionEstadoOt>();

    public virtual ICollection<TransicionEstadoOt> TransicionesDeDestino { get; set; } = new List<TransicionEstadoOt>();

    public virtual ICollection<HistorialEstadoOt> HistorialEstadoOts { get; set; } = new List<HistorialEstadoOt>();
}
