using Api.DTOs.Repuestos;

namespace Api.DTOs.Reportes;

public class IndicadoresDto
{
    public int TotalOrdenes { get; init; }
    public List<CantidadPorGrupoDto> OrdenesPorEstado { get; init; } = new();
    public List<CantidadPorGrupoDto> OrdenesPorPrioridad { get; init; } = new();
    public List<CantidadPorGrupoDto> OrdenesPorTipo { get; init; } = new();
    public List<CantidadPorGrupoDto> OrdenesPorTecnico { get; init; } = new();
    public List<CantidadPorGrupoDto> ActivosConMasIntervenciones { get; init; } = new();
    public List<RepuestoDto> RepuestosBajoStock { get; init; } = new();
}
