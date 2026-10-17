namespace Api.DTOs.Reportes;

public class FiltrosAnaliticaDto
{
    public DateOnly? FechaDesde { get; init; }
    public DateOnly? FechaHasta { get; init; }
    public long? ClienteId { get; init; }
    public long? ActivoId { get; init; }
    public long? TecnicoId { get; init; }
    public string? Agrupacion { get; init; }
    public int? Dias { get; init; }
    public int? Minimo { get; init; }
}

public class PuntoTendenciaDto
{
    public required string Periodo { get; init; }
    public int Preventivas { get; init; }
    public int Correctivas { get; init; }
    public int Total { get; init; }
}

public class TendenciasDto
{
    public required string Agrupacion { get; init; }
    public required List<PuntoTendenciaDto> Puntos { get; init; }
    public int TotalPeriodo { get; init; }

    /// <summary>Total del período inmediatamente anterior, del mismo largo; solo si se indicaron las dos fechas.</summary>
    public int? TotalPeriodoAnterior { get; init; }
}

public class DuracionDto
{
    public int Cantidad { get; init; }
    public double? PromedioHoras { get; init; }
    public double? MedianaHoras { get; init; }
    public double? Percentil90Horas { get; init; }
}

public class EtapaDto
{
    public required string Estado { get; init; }
    public int Cantidad { get; init; }
    public double PromedioHoras { get; init; }
}

public class CumplimientoDto
{
    /// <summary>Órdenes cerradas con fecha prevista de ejecución.</summary>
    public int Evaluadas { get; init; }

    public int EnPlazo { get; init; }

    /// <summary>Porcentaje de órdenes cerradas a más tardar en la fecha prevista; <c>null</c> si no hay órdenes evaluadas.</summary>
    public double? PorcentajeEnPlazo { get; init; }

    public double? AtrasoPromedioDias { get; init; }
}

public class TiemposDto
{
    public required DuracionDto Duracion { get; init; }
    public required List<EtapaDto> Etapas { get; init; }
    public required CumplimientoDto Cumplimiento { get; init; }
}

public class RepuestoUsadoDto
{
    public long RepuestoId { get; init; }
    public required string Codigo { get; init; }
    public required string Nombre { get; init; }
    public int Ordenes { get; init; }
}

public class RepuestosUsadosDto
{
    public required List<RepuestoUsadoDto> Elementos { get; init; }
}

public class ReincidenciaDto
{
    public long ActivoId { get; init; }
    public required string NombreActivo { get; init; }
    public required string Cliente { get; init; }
    public int Correctivas { get; init; }
    public int Reincidencias { get; init; }
    public double? DiasPromedioEntreIntervenciones { get; init; }
    public DateTime UltimaIntervencion { get; init; }
}

public class ReincidenciasDto
{
    public int Dias { get; init; }
    public int Minimo { get; init; }
    public required List<ReincidenciaDto> Elementos { get; init; }
}

public class AlertaMaquinariaDto
{
    /// <summary><c>sin_asignar</c>, <c>demorada_para_iniciar</c>, <c>pausada_demasiado</c> o <c>maquina_detenida</c>.</summary>
    public required string Tipo { get; init; }

    /// <summary><c>critica</c> si superó el doble del umbral; si no, <c>alta</c>.</summary>
    public required string Gravedad { get; init; }

    public long OrdenId { get; init; }
    public long ActivoId { get; init; }
    public required string NombreActivo { get; init; }
    public required string Cliente { get; init; }
    public required string Estado { get; init; }
    public required string Prioridad { get; init; }
    public double HorasTranscurridas { get; init; }
    public int UmbralHoras { get; init; }
}

public class AlertasMaquinariaDto
{
    public int Total { get; init; }
    public int Criticas { get; init; }
    public required List<AlertaMaquinariaDto> Alertas { get; init; }
}
