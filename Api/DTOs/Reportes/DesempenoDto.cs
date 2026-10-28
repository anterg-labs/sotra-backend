namespace Api.DTOs.Reportes;

/// <summary>
/// Desempeño operativo: cómo se comportan las órdenes, los procesos y la maquinaria en el período (RD-10). No evalúa
/// personas: no hay indicadores por técnico ni comparaciones entre personas.
/// </summary>
public class DesempenoDto
{
    /// <summary>Órdenes consideradas.</summary>
    public required ResumenDeOrdenesDto Ordenes { get; init; }
    /// <summary>Tiempos de respuesta, ejecución y revisión.</summary>
    public required TiemposDeProcesoDto Tiempos { get; init; }
    /// <summary>Pausas, devoluciones y reasignaciones.</summary>
    public required FlujoDeProcesoDto Flujo { get; init; }
    /// <summary>Resumen por tipo de máquina y por activo.</summary>
    public required MaquinariaDto Maquinaria { get; init; }
    /// <summary>Tiempo estimado frente al real de las órdenes que tienen estimado.</summary>
    public required EstimacionDto Estimacion { get; init; }
    /// <summary>Tiempo transcurrido de creación a cierre, incluidas pausas, por tipo de mantenimiento.</summary>
    public required PlanificacionDto Planificacion { get; init; }
}

public class HorasMantenimientoDto
{
    public int OrdenesCerradas { get; init; }
    public double HorasPreventivas { get; init; }
    public double HorasCorrectivas { get; init; }
    public double HorasTotales { get; init; }
    /// <summary>Horas preventivas / horas totales × 100; null cuando el total es cero.</summary>
    public double? PorcentajePlanificado { get; init; }
}

public class PlanificacionPorMesDto
{
    public required string Periodo { get; init; }
    public required HorasMantenimientoDto Resumen { get; init; }
}

public class PlanificacionPorTipoDto
{
    public long Id { get; init; }
    public required string Nombre { get; init; }
    public required HorasMantenimientoDto Resumen { get; init; }
}

public class PlanificacionDto
{
    public required RangoDeFechasDto Periodo { get; init; }
    public required HorasMantenimientoDto Resumen { get; init; }
    public required List<PlanificacionPorMesDto> PorMes { get; init; }
    public required List<PlanificacionPorTipoDto> PorTipo { get; init; }
}

public class ResumenDeOrdenesDto
{
    /// <summary>Órdenes creadas en el período (según los filtros).</summary>
    public int Creadas { get; init; }

    /// <summary>De ellas, las que ya se cerraron.</summary>
    public int Cerradas { get; init; }

    /// <summary>Órdenes eliminadas.</summary>
    public int Eliminadas { get; init; }

    /// <summary>Pendientes, en curso o en revisión: todavía no se cerraron ni eliminaron.</summary>
    public int Abiertas { get; init; }

    /// <summary>Órdenes preventivas.</summary>
    public int Preventivas { get; init; }
    /// <summary>Órdenes correctivas.</summary>
    public int Correctivas { get; init; }

    /// <summary>Preventivas sobre el total de órdenes; <c>null</c> si no hay órdenes.</summary>
    public double? PorcentajePreventivas { get; init; }
}

public class TiemposDeProcesoDto
{
    /// <summary>De la creación al inicio de la ejecución.</summary>
    public required DuracionDto Respuesta { get; init; }

    /// <summary>Del inicio a la primera vez que la orden llega a revisión.</summary>
    public required DuracionDto Ejecucion { get; init; }

    /// <summary>De la primera revisión al cierre.</summary>
    public required DuracionDto Revision { get; init; }
}

public class FlujoDeProcesoDto
{
    /// <summary>Pausas de las órdenes.</summary>
    public required PausasDto Pausas { get; init; }
    /// <summary>Devoluciones de revisión a pendiente.</summary>
    public required DevolucionesDto Devoluciones { get; init; }
    /// <summary>Cambios de técnico.</summary>
    public required ReasignacionesDto Reasignaciones { get; init; }
}

public class MotivoDto
{
    /// <summary>Texto.</summary>
    public required string Texto { get; init; }
    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    public int Cantidad { get; init; }
}

public class PausasDto
{
    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    public int Cantidad { get; init; }
    /// <summary>Órdenes con al menos una pausa.</summary>
    public int OrdenesConPausa { get; init; }

    /// <summary>Horas promedio que dura una pausa (hasta el cambio siguiente de la orden); <c>null</c> si no hay pausas terminadas.</summary>
    public double? DuracionPromedioHoras { get; init; }

    /// <summary>Los motivos más frecuentes (mismo texto sin distinguir mayúsculas).</summary>
    public required List<MotivoDto> Motivos { get; init; }

    /// <summary>Pausas y horas en pausa por motivo codificado; las pausas sin código van como `sin_codificar`.</summary>
    public required List<PausaPorMotivoDto> PorMotivo { get; init; }
}

public class DevolucionesDto
{
    /// <summary>Veces que una orden volvió de revisión a pendiente (retrabajo).</summary>
    public int Cantidad { get; init; }

    /// <summary>Órdenes devueltas.</summary>
    public int OrdenesDevueltas { get; init; }

    /// <summary>Órdenes devueltas sobre las órdenes creadas; <c>null</c> si no hay órdenes.</summary>
    public double? PorcentajeDeOrdenes { get; init; }

    /// <summary>Motivos más frecuentes con su cantidad.</summary>
    public required List<MotivoDto> Motivos { get; init; }
}

public class ReasignacionesDto
{
    /// <summary>Cambios de técnico de una orden que ya tenía uno.</summary>
    public int Cantidad { get; init; }

    /// <summary>Órdenes con reasignación.</summary>
    public int OrdenesReasignadas { get; init; }
}

public class MaquinariaDto
{
    /// <summary>Detalle por tipo de máquina.</summary>
    public required List<MaquinariaItemDto> PorTipo { get; init; }

    /// <summary>Los activos con más horas de parada acumuladas, con el porcentaje acumulado del total (Pareto).</summary>
    public required List<MaquinariaItemDto> Activos { get; init; }

    /// <summary>Detalle por modelo de máquina, de más a menos horas de parada.</summary>
    public required List<MaquinariaItemDto> PorModelo { get; init; }
}

public class MaquinariaItemDto
{
    /// <summary>Identificador del registro.</summary>
    public long Id { get; init; }
    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }
    /// <summary>Nombre del cliente.</summary>
    public string? Cliente { get; init; }
    /// <summary>Órdenes consideradas.</summary>
    public int Ordenes { get; init; }
    /// <summary>Órdenes correctivas.</summary>
    public int Correctivas { get; init; }
    /// <summary>Órdenes preventivas.</summary>
    public int Preventivas { get; init; }

    /// <summary>Horas promedio de ejecución de las correctivas, del inicio a la revisión.</summary>
    public double? HorasPromedioEjecucion { get; init; }

    /// <summary>Horas de parada acumuladas: de la creación al cierre de las correctivas cerradas.</summary>
    public double HorasParada { get; init; }

    /// <summary>Correctivas cerradas (base del tiempo medio de reparación).</summary>
    public int CorrectivasCerradas { get; init; }

    /// <summary>Tiempo medio de reparación en horas: parada acumulada sobre correctivas cerradas; vacío si no hay.</summary>
    public double? TiempoMedioReparacionHoras { get; init; }

    /// <summary>Porcentaje de la parada total del período que representa este registro.</summary>
    public double? PorcentajeDeLaParada { get; init; }

    /// <summary>Porcentaje acumulado de la parada total hasta este registro (lista ordenada de mayor a menor).</summary>
    public double? PorcentajeAcumulado { get; init; }

    /// <summary>Activos del modelo en servicio (solo en el detalle por modelo).</summary>
    public int? ActivosEnServicio { get; init; }

    /// <summary>Costo de la parada (horas de parada por el costo por hora del tipo de máquina); vacío si no se cargó el costo.</summary>
    public decimal? CostoParada { get; init; }
}
