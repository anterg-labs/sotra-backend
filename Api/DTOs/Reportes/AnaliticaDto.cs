namespace Api.DTOs.Reportes;

public class FiltrosAnaliticaDto
{
    /// <summary>Inicio del período (`aaaa-mm-dd`).</summary>
    public DateOnly? FechaDesde { get; init; }
    /// <summary>Fin del período (`aaaa-mm-dd`).</summary>
    public DateOnly? FechaHasta { get; init; }
    /// <summary>Identificador del cliente.</summary>
    public long? ClienteId { get; init; }
    /// <summary>Identificador del activo.</summary>
    public long? ActivoId { get; init; }
    /// <summary>Agrupación de los puntos (por ejemplo por día o por semana).</summary>
    public string? Agrupacion { get; init; }
    /// <summary>Cantidad de días.</summary>
    public int? Dias { get; init; }
    /// <summary>Mínimo.</summary>
    public int? Minimo { get; init; }
}

public class PuntoTendenciaDto
{
    /// <summary>Período del punto.</summary>
    public required string Periodo { get; init; }
    /// <summary>Órdenes preventivas.</summary>
    public int Preventivas { get; init; }
    /// <summary>Órdenes correctivas.</summary>
    public int Correctivas { get; init; }
    /// <summary>Total.</summary>
    public int Total { get; init; }
    /// <summary>Órdenes cerradas en el período (por fecha de cierre).</summary>
    public int Cerradas { get; init; }
}

public class TendenciasDto
{
    /// <summary>Agrupación de los puntos (por ejemplo por día o por semana).</summary>
    public required string Agrupacion { get; init; }
    /// <summary>Puntos de la serie.</summary>
    public required List<PuntoTendenciaDto> Puntos { get; init; }
    /// <summary>Total del período.</summary>
    public int TotalPeriodo { get; init; }

    /// <summary>Total del período inmediatamente anterior, del mismo largo; solo si se indicaron las dos fechas.</summary>
    public int? TotalPeriodoAnterior { get; init; }
}

public class DuracionDto
{
    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    public int Cantidad { get; init; }
    /// <summary>Promedio en horas.</summary>
    public double? PromedioHoras { get; init; }
    /// <summary>Mediana en horas.</summary>
    public double? MedianaHoras { get; init; }
    /// <summary>Percentil 90 en horas.</summary>
    public double? Percentil90Horas { get; init; }
}

public class EtapaDto
{
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }
    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    public int Cantidad { get; init; }
    /// <summary>Promedio en horas.</summary>
    public double PromedioHoras { get; init; }
}

public class CumplimientoDto
{
    /// <summary>Órdenes cerradas con fecha prevista de ejecución.</summary>
    public int Evaluadas { get; init; }

    /// <summary>Órdenes terminadas en plazo.</summary>
    public int EnPlazo { get; init; }

    /// <summary>Porcentaje de órdenes cerradas a más tardar en la fecha prevista; <c>null</c> si no hay órdenes evaluadas.</summary>
    public double? PorcentajeEnPlazo { get; init; }

    /// <summary>Atraso promedio en días.</summary>
    public double? AtrasoPromedioDias { get; init; }
}

public class TiemposDto
{
    /// <summary>Duración.</summary>
    public required DuracionDto Duracion { get; init; }
    /// <summary>Etapas del proceso con sus tiempos.</summary>
    public required List<EtapaDto> Etapas { get; init; }
    /// <summary>Cumplimiento de los plazos.</summary>
    public required CumplimientoDto Cumplimiento { get; init; }
    /// <summary>Cumplimiento de la fecha prevista por tipo de orden (preventivo y correctivo).</summary>
    public required List<CumplimientoGrupoDto> CumplimientoPorTipoDeOrden { get; init; }
    /// <summary>Cumplimiento del plan preventivo por tipo de máquina.</summary>
    public required List<CumplimientoGrupoDto> CumplimientoPreventivoPorTipoDeMaquina { get; init; }
}

public class RepuestoUsadoDto
{
    /// <summary>Identificador del repuesto.</summary>
    public long RepuestoId { get; init; }
    /// <summary>Código.</summary>
    public required string Codigo { get; init; }
    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }
    /// <summary>Órdenes consideradas.</summary>
    public int Ordenes { get; init; }
}

public class RepuestosUsadosDto
{
    /// <summary>Elementos de la página.</summary>
    public required List<RepuestoUsadoDto> Elementos { get; init; }
}

public class ReincidenciaDto
{
    /// <summary>Identificador del activo.</summary>
    public long ActivoId { get; init; }
    /// <summary>Nombre del activo (o su número de serie si no tiene nombre).</summary>
    public required string NombreActivo { get; init; }
    /// <summary>Nombre del cliente.</summary>
    public required string Cliente { get; init; }
    /// <summary>Órdenes correctivas.</summary>
    public int Correctivas { get; init; }
    /// <summary>Activos con intervenciones repetidas.</summary>
    public int Reincidencias { get; init; }
    /// <summary>Días promedio entre intervenciones.</summary>
    public double? DiasPromedioEntreIntervenciones { get; init; }
    /// <summary>Fecha de la última intervención.</summary>
    public DateTime UltimaIntervencion { get; init; }
}

public class ReincidenciasDto
{
    /// <summary>Cantidad de días.</summary>
    public int Dias { get; init; }
    /// <summary>Mínimo.</summary>
    public int Minimo { get; init; }
    /// <summary>Elementos de la página.</summary>
    public required List<ReincidenciaDto> Elementos { get; init; }
}

public class AlertaMaquinariaDto
{
    /// <summary><c>sin_asignar</c>, <c>demorada_para_iniciar</c>, <c>pausada_demasiado</c> o <c>maquina_detenida</c>.</summary>
    public required string Tipo { get; init; }

    /// <summary><c>critica</c> si superó el doble del umbral; si no, <c>alta</c>.</summary>
    public required string Gravedad { get; init; }

    /// <summary>Identificador de la orden.</summary>
    public long OrdenId { get; init; }
    /// <summary>Identificador del activo.</summary>
    public long ActivoId { get; init; }
    /// <summary>Nombre del activo (o su número de serie si no tiene nombre).</summary>
    public required string NombreActivo { get; init; }
    /// <summary>Nombre del cliente.</summary>
    public required string Cliente { get; init; }
    /// <summary>Estado del registro (`activo` o `inactivo`) o, en órdenes, valor del estado vigente del catálogo.</summary>
    public required string Estado { get; init; }
    /// <summary>Prioridad: `alta`, `media` o `baja`.</summary>
    public required string Prioridad { get; init; }
    /// <summary>Horas transcurridas.</summary>
    public double HorasTranscurridas { get; init; }
    /// <summary>Umbral de horas que se superó.</summary>
    public int UmbralHoras { get; init; }
}

public class AlertasMaquinariaDto
{
    /// <summary>Total.</summary>
    public int Total { get; init; }
    /// <summary>Alertas críticas.</summary>
    public int Criticas { get; init; }
    /// <summary>Alertas.</summary>
    public required List<AlertaMaquinariaDto> Alertas { get; init; }
}
