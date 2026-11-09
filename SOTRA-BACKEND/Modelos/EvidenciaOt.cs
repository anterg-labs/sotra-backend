namespace SOTRA_BACKEND.Modelos;

public partial class EvidenciaOt
{
    public long IdEvidencia { get; set; }

    public long IdOrden { get; set; }

    public long? IdUsuario { get; set; }

    public string Tipo { get; set; } = null!;

    public string? Observacion { get; set; }

    public DateOnly FechaRegistro { get; set; }

    public DateTime CreadoEn { get; set; }

    public string? NombreArchivo { get; set; }

    public string? TipoContenido { get; set; }

    /// <summary>Contenido de evidencias guardadas en la base, de antes de R2 o sin R2 configurado.</summary>
    public byte[]? Archivo { get; set; }

    /// <summary>Clave del archivo en R2 (evidencias/{idOrden}/...). Si tiene valor, Archivo queda vacío.</summary>
    public string? ClaveArchivo { get; set; }
}
