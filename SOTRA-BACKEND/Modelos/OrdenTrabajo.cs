using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class OrdenTrabajo
{
    public long IdOrden { get; set; }

    public long IdJefeMantenimiento { get; set; }

    public long? IdTecnico { get; set; }

    public long IdActivo { get; set; }

    public long IdCliente { get; set; }

    public string Tipo { get; set; } = null!;

    public string Descripcion { get; set; } = null!;

    public DateTime FechaCreada { get; set; }

    public DateTime? FechaInicio { get; set; }

    public DateTime? FechaCierre { get; set; }

    public string? Resultado { get; set; }

    public virtual ICollection<HistorialEstadoOt> HistorialEstadoOts { get; set; } = new List<HistorialEstadoOt>();

    public virtual Activo IdActivoNavigation { get; set; } = null!;

    public virtual Cliente IdClienteNavigation { get; set; } = null!;

    public virtual JefeMantenimiento IdJefeMantenimientoNavigation { get; set; } = null!;

    public virtual Tecnico? IdTecnicoNavigation { get; set; }

    public virtual ICollection<OtRepuesto> OtRepuestos { get; set; } = new List<OtRepuesto>();
}
