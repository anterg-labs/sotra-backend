namespace Api.DTOs.OrdenesTrabajo;

public class EvidenciaDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Identificador de la orden de trabajo asociada.</summary>
    public long OrdenTrabajoId { get; init; }
    /// <summary>Tipo; los valores posibles dependen del recurso (por ejemplo `preventivo` o `correctivo`).</summary>
    public required string Tipo { get; init; }
    /// <summary>Observación.</summary>
    public string Observacion { get; init; } = "";
    /// <summary>Fecha y hora de registro (ISO 8601, UTC).</summary>
    public required string FechaRegistro { get; init; }
    /// <summary>Fecha y hora de creación (ISO 8601, UTC).</summary>
    public DateTime CreadoEn { get; init; }
    /// <summary>Nombre del archivo.</summary>
    public string? NombreArchivo { get; init; }
    /// <summary>Tipo de contenido (MIME) del archivo.</summary>
    public string? TipoContenido { get; init; }
    /// <summary>Indica si hay un archivo para descargar.</summary>
    public bool TieneArchivo { get; init; }
}
