namespace Api.DTOs.OrdenesTrabajo;

public class EvidenciaDto
{
    public long Id { get; init; }
    public long OrdenTrabajoId { get; init; }
    public required string Tipo { get; init; }
    public string Observacion { get; init; } = "";
    public required string FechaRegistro { get; init; }
    public DateTime CreadoEn { get; init; }
    public string? NombreArchivo { get; init; }
    public string? TipoContenido { get; init; }
    public bool TieneArchivo { get; init; }
}
