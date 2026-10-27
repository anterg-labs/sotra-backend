using System;
using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class HistorialEstadoOt
{
    public long IdHEstadoOt { get; set; }

    public long IdOrden { get; set; }

    public long? IdMotivoEs { get; set; }

    public long IdEstadoOt { get; set; }

    public DateTime FechaHora { get; set; }

    public string? Observacion { get; set; }

    /// <summary>Estado del que se vino; vacío en el alta de la orden.</summary>
    public long? IdEstadoAnterior { get; set; }

    /// <summary>Quién hizo el cambio; vacío si no se pudo identificar (cambios anteriores al registro o sin sesión).</summary>
    public long? IdUsuario { get; set; }

    public virtual EstadoOt? IdEstadoAnteriorNavigation { get; set; }

    public virtual Usuario? IdUsuarioNavigation { get; set; }

    public virtual EstadoOt IdEstadoOtNavigation { get; set; } = null!;

    public virtual MotivoEstadoOt? IdMotivoEsNavigation { get; set; }

    public virtual OrdenTrabajo IdOrdenNavigation { get; set; } = null!;
}
