namespace Api.DTOs.OrdenesTrabajo;

public class OrdenTrabajoDto
{
    public long Id { get; init; }
    public long ClienteId { get; init; }
    public required string NombreCliente { get; init; }
    public long ActivoId { get; init; }
    public required string NombreActivo { get; init; }
    public long? TecnicoId { get; init; }
    public string? NombreTecnico { get; init; }
    public required string Tipo { get; init; }
    public required string Prioridad { get; init; }
    public required string Descripcion { get; init; }
    public string Observaciones { get; init; } = "";
    public required string FechaPrevistaEjecucion { get; init; }
    public required string Estado { get; init; }
    public DateTime CreadoEn { get; init; }
    public DateTime? FechaInicio { get; init; }
    public DateTime? FechaCierre { get; init; }
    public List<string> Sugerencias { get; init; } = new();
    public List<RepuestoOrdenDto> Repuestos { get; init; } = new();
    public List<string> EstadosSiguientes { get; init; } = new();
    public List<HistorialEstadoDto> HistorialEstados { get; init; } = new();
}
