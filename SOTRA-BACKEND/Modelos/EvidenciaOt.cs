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

    public byte[]? Archivo { get; set; }
}
