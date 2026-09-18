using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class OrdenTrabajo
{
    public long IdOrden { get; set; }

    public long IdActivo { get; set; }

    public long IdCliente { get; set; }

    public bool Tipo { get; set; }

    public string Descripcion { get; set; } = null!;

    public DateTime FechaCreada { get; set; }

    public DateTime? FechaInicio { get; set; }

    public DateTime? FechaCierre { get; set; }

    public string? Resultado { get; set; }

    public string? RecomendacionIa { get; set; }

    public long IdUsuarioResponsable { get; set; }

    public long? IdUsuarioTecnico { get; set; }

    public virtual ICollection<HistorialEstadoOt> HistorialEstadoOts { get; set; } = new List<HistorialEstadoOt>();

    public virtual Activo IdActivoNavigation { get; set; } = null!;

    public virtual Cliente IdClienteNavigation { get; set; } = null!;

    public virtual Usuario IdUsuarioResponsableNavigation { get; set; } = null!;

    public virtual Usuario? IdUsuarioTecnicoNavigation { get; set; }

    public virtual ICollection<OtRepuesto> OtRepuestos { get; set; } = new List<OtRepuesto>();
}
