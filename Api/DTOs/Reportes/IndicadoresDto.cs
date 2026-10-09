using Api.DTOs.OrdenesTrabajo;
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
    public double? PromedioHorasHastaInicio { get; init; }
    public double? PromedioHorasHastaCierre { get; init; }
    public int OrdenesAltaPrioridadPendientes { get; init; }
    public int OrdenesPendientesDeValidacion { get; init; }
    public List<OrdenTrabajoDto> OrdenesRecientes { get; init; } = new();
}
