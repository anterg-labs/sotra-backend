namespace Api.DTOs.Reportes;

/// <summary>
/// Desempeño operativo: cómo se comportan las órdenes, los procesos y la maquinaria en el período (RD-10). No evalúa
/// personas: no hay indicadores por técnico ni comparaciones entre personas.
/// </summary>
public class DesempenoDto
{
    public required ResumenDeOrdenesDto Ordenes { get; init; }
    public required TiemposDeProcesoDto Tiempos { get; init; }
    public required FlujoDeProcesoDto Flujo { get; init; }
    public required MaquinariaDto Maquinaria { get; init; }
}

public class ResumenDeOrdenesDto
{
    /// <summary>Órdenes creadas en el período (según los filtros).</summary>
    public int Creadas { get; init; }

    /// <summary>De ellas, las que ya se cerraron.</summary>
    public int Cerradas { get; init; }

    public int Eliminadas { get; init; }

    /// <summary>Pendientes, en curso o en revisión: todavía no se cerraron ni eliminaron.</summary>
    public int Abiertas { get; init; }

    public int Preventivas { get; init; }
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
    public required PausasDto Pausas { get; init; }
    public required DevolucionesDto Devoluciones { get; init; }
    public required ReasignacionesDto Reasignaciones { get; init; }
}

public class MotivoDto
{
    public required string Texto { get; init; }
    public int Cantidad { get; init; }
}

public class PausasDto
{
    public int Cantidad { get; init; }
    public int OrdenesConPausa { get; init; }

    /// <summary>Horas promedio que dura una pausa (hasta el cambio siguiente de la orden); <c>null</c> si no hay pausas terminadas.</summary>
    public double? DuracionPromedioHoras { get; init; }

    /// <summary>Los motivos más frecuentes (mismo texto sin distinguir mayúsculas).</summary>
    public required List<MotivoDto> Motivos { get; init; }
}

public class DevolucionesDto
{
    /// <summary>Veces que una orden volvió de revisión a pendiente (retrabajo).</summary>
    public int Cantidad { get; init; }

    public int OrdenesDevueltas { get; init; }

    /// <summary>Órdenes devueltas sobre las órdenes creadas; <c>null</c> si no hay órdenes.</summary>
    public double? PorcentajeDeOrdenes { get; init; }

    public required List<MotivoDto> Motivos { get; init; }
}

public class ReasignacionesDto
{
    /// <summary>Cambios de técnico de una orden que ya tenía uno.</summary>
    public int Cantidad { get; init; }

    public int OrdenesReasignadas { get; init; }
}

public class MaquinariaDto
{
    public required List<MaquinariaItemDto> PorTipo { get; init; }

    /// <summary>Los activos con más horas de parada acumuladas.</summary>
    public required List<MaquinariaItemDto> Activos { get; init; }
}

public class MaquinariaItemDto
{
    public long Id { get; init; }
    public required string Nombre { get; init; }
    public string? Cliente { get; init; }
    public int Ordenes { get; init; }
    public int Correctivas { get; init; }
    public int Preventivas { get; init; }

    /// <summary>Horas promedio de ejecución de las correctivas, del inicio a la revisión.</summary>
    public double? HorasPromedioEjecucion { get; init; }

    /// <summary>Horas de parada acumuladas: de la creación al cierre de las correctivas cerradas.</summary>
    public double HorasParada { get; init; }
}
