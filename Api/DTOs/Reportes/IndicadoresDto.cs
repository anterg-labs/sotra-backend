using Api.DTOs.OrdenesTrabajo;
using Api.DTOs.Repuestos;

namespace Api.DTOs.Reportes;

public class IndicadoresDto
{
    /// <summary>Total de órdenes.</summary>
    public int TotalOrdenes { get; init; }
    /// <summary>Órdenes por estado.</summary>
    public List<CantidadPorGrupoDto> OrdenesPorEstado { get; init; } = new();
    /// <summary>Órdenes por prioridad.</summary>
    public List<CantidadPorGrupoDto> OrdenesPorPrioridad { get; init; } = new();
    /// <summary>Órdenes por tipo de mantenimiento.</summary>
    public List<CantidadPorGrupoDto> OrdenesPorTipo { get; init; } = new();
    /// <summary>Activos con más intervenciones.</summary>
    public List<CantidadPorGrupoDto> ActivosConMasIntervenciones { get; init; } = new();
    /// <summary>Repuestos con stock por debajo del mínimo.</summary>
    public List<RepuestoDto> RepuestosBajoStock { get; init; } = new();
    /// <summary>Promedio de horas hasta el inicio.</summary>
    public double? PromedioHorasHastaInicio { get; init; }
    /// <summary>Promedio de horas hasta el cierre.</summary>
    public double? PromedioHorasHastaCierre { get; init; }
    /// <summary>Órdenes de prioridad alta pendientes.</summary>
    public int OrdenesAltaPrioridadPendientes { get; init; }
    /// <summary>Órdenes terminadas pendientes de validación.</summary>
    public int OrdenesPendientesDeValidacion { get; init; }
    /// <summary>Órdenes más recientes.</summary>
    public List<OrdenTrabajoDto> OrdenesRecientes { get; init; } = new();
}
