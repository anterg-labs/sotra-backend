using System.Collections.Generic;

namespace SOTRA_BACKEND.Modelos;

public partial class HistorialRecomendacionIa
{
    public long IdRecomendacionIa { get; set; }

    public string Recomendacion { get; set; } = null!;

    public long? IdOrden { get; set; }

    public bool Estado { get; set; }

    public Guid? Sesion { get; set; }

    public string? Consulta { get; set; }

    public int? NumeroIntento { get; set; }

    public DateTime FechaGeneracion { get; set; }

    public string Resultado { get; set; } = null!;

    /// <summary>Cómo se redactó: "ia", "sin_ia" (proveedor caído) o "descartada" (no pasó el control de números).</summary>
    public string Modo { get; set; } = "ia";

    public virtual OrdenTrabajo? IdOrdenNavigation { get; set; }
}
