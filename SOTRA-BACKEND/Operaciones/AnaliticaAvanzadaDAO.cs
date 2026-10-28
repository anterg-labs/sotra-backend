using Microsoft.EntityFrameworkCore;
using Npgsql;
using SOTRA_BACKEND.Contexto;

namespace SOTRA_BACKEND.Operaciones;

public sealed class CierrePeriodoFila
{
    public DateTime Periodo { get; set; }
    public int Cerradas { get; set; }
}

public sealed class CumplimientoPorTipoFila
{
    public long TipoMaquinaId { get; set; }
    public string TipoMaquina { get; set; } = "";
    public bool EsPreventiva { get; set; }
    public int Evaluadas { get; set; }
    public int EnPlazo { get; set; }
}

public sealed class PausaPorMotivoFila
{
    public string Codigo { get; set; } = "";
    public string Motivo { get; set; } = "";
    public int Cantidad { get; set; }
    public double Horas { get; set; }
}

public sealed class KpisFila
{
    public int Creadas { get; set; }
    public int Preventivas { get; set; }
    public int Correctivas { get; set; }
    public int PreventivasCerradas { get; set; }
    public int PreventivasEnPlazo { get; set; }
    public int CorrectivasCerradas { get; set; }
    public double HorasParada { get; set; }
    public double HorasPreventivas { get; set; }
}

public sealed class KpisPorMesFila
{
    public DateTime Periodo { get; set; }
    public int Creadas { get; set; }
    public int Preventivas { get; set; }
    public int Correctivas { get; set; }
    public int PreventivasCerradas { get; set; }
    public int PreventivasEnPlazo { get; set; }
    public int CorrectivasCerradas { get; set; }
    public double HorasParada { get; set; }
    public double HorasPreventivas { get; set; }
}

public sealed class PlanificacionFila
{
    public DateTime Periodo { get; set; }
    public long TipoMaquinaId { get; set; }
    public string TipoMaquina { get; set; } = "";
    public int OrdenesCerradas { get; set; }
    public double HorasPreventivas { get; set; }
    public double HorasCorrectivas { get; set; }
}

public sealed class EnvejecimientoFila
{
    public int HastaTresDias { get; set; }
    public int CuatroASiete { get; set; }
    public int OchoATreinta { get; set; }
    public int TreintaYUnoANoventa { get; set; }
    public int MasDeNoventa { get; set; }
    public int Total { get; set; }
}

public sealed class OrdenAntiguaFila
{
    public long OrdenId { get; set; }
    public string NombreActivo { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string Estado { get; set; } = "";
    public double EdadDias { get; set; }
}

public sealed class EstimacionFila
{
    public int Ordenes { get; set; }
    public double? HorasEstimadas { get; set; }
    public double? HorasReales { get; set; }
    public double? DesviacionPromedioPorcentaje { get; set; }
}

public sealed class IntervaloFila
{
    public long ActivoId { get; set; }
    public string NombreActivo { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string Modelo { get; set; } = "";
    public string UnidadUso { get; set; } = "horas";
    public int Numero { get; set; }
    public double Dias { get; set; }
    public double? UsoEntreFallas { get; set; }
}

/// <summary>Fila de una regla de aviso con el conteo que necesita para decidir si se dispara.</summary>
public sealed class ConteoPorActivoFila
{
    public long ActivoId { get; set; }
    public string NombreActivo { get; set; } = "";
    public string Cliente { get; set; } = "";
    public int EnVentana { get; set; }
    public int EnVentanaCritica { get; set; }
}

public sealed class ProporcionFila
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public int Parte { get; set; }
    public int Total { get; set; }
}

public sealed class CantidadFila
{
    public int Cantidad { get; set; }
    public int Criticas { get; set; }
}

public sealed class TiempoReparacionFila
{
    public long TipoMaquinaId { get; set; }
    public string TipoMaquina { get; set; } = "";
    public double? PromedioReciente { get; set; }
    public int MuestraReciente { get; set; }
    public double? PromedioHistorico { get; set; }
    public int MuestraHistorica { get; set; }
}

public sealed class ParadaActivoFila
{
    public long ActivoId { get; set; }
    public double Horas { get; set; }
}

/// <summary>Consultas de análisis avanzado: comparativas, envejecimiento, intervalos entre correctivas y avisos.</summary>
public class AnaliticaAvanzadaDAO(SotraContext contexto)
{
    private const string Filtros = AnaliticaDAO.FiltrosDeOrden;

    private const string FaseActual = """
        JOIN LATERAL (
            SELECT e.fase, e.nombre, h.fecha_hora
            FROM operativo.historial_estado_ot AS h
            JOIN operativo.estado_ot AS e ON e.id_estado_ot = h.id_estado_ot
            WHERE h.id_orden = o.id_orden
            ORDER BY h.fecha_hora DESC, h.id_h_estado_ot DESC
            LIMIT 1
        ) AS actual ON TRUE
        """;

    private const string SqlCierres = """
        SELECT date_trunc(@unidad, o.fecha_cierre AT TIME ZONE 'UTC') AS "Periodo", count(*)::int AS "Cerradas"
        FROM operativo.orden_trabajo AS o
        WHERE o.fecha_cierre IS NOT NULL
          AND (@desde IS NULL OR o.fecha_cierre >= @desde)
          AND (@hasta IS NULL OR o.fecha_cierre < @hasta)
          AND (@clienteId IS NULL OR o.id_cliente = @clienteId)
          AND (@activoId IS NULL OR o.id_activo = @activoId)
        GROUP BY 1
        ORDER BY 1
        """;

    private const string SqlCumplimientoPorTipo = $"""
        SELECT tm.id_tipo_maquina AS "TipoMaquinaId", tm.nombre AS "TipoMaquina", o.tipo AS "EsPreventiva",
               count(*)::int AS "Evaluadas",
               (count(*) FILTER (WHERE (o.fecha_cierre AT TIME ZONE 'UTC')::date <= o.fecha_prevista_ejecucion))::int AS "EnPlazo"
        FROM operativo.orden_trabajo AS o
        JOIN operativo.activo AS a ON a.id_activo = o.id_activo
        JOIN operativo.modelo_maquina AS mm ON mm.id_modelo_maquina = a.id_modelo_maquina
        JOIN operativo.tipo_maquina AS tm ON tm.id_tipo_maquina = mm.id_tipo_maquina
        WHERE o.fecha_cierre IS NOT NULL {Filtros}
        GROUP BY tm.id_tipo_maquina, tm.nombre, o.tipo
        ORDER BY tm.nombre, o.tipo
        """;

    private const string SqlPausasPorMotivo = $"""
        WITH cambios AS (
            SELECT h.id_orden, h.fecha_hora, h.id_motivo_es, h.id_estado_anterior, h.id_estado_ot,
                   LEAD(h.fecha_hora) OVER (PARTITION BY h.id_orden ORDER BY h.fecha_hora, h.id_h_estado_ot) AS siguiente
            FROM operativo.historial_estado_ot AS h
            JOIN operativo.orden_trabajo AS o ON o.id_orden = h.id_orden
            WHERE TRUE {Filtros}
        )
        SELECT COALESCE(m.codigo, 'sin_codificar') AS "Codigo",
               COALESCE(m.descripcion, 'Sin motivo codificado') AS "Motivo",
               count(*)::int AS "Cantidad",
               COALESCE(sum(EXTRACT(EPOCH FROM (c.siguiente - c.fecha_hora)) / 3600.0) FILTER (WHERE c.siguiente IS NOT NULL), 0)::float8 AS "Horas"
        FROM cambios AS c
        JOIN operativo.estado_ot AS ea ON ea.id_estado_ot = c.id_estado_anterior
        JOIN operativo.estado_ot AS en ON en.id_estado_ot = c.id_estado_ot
        LEFT JOIN operativo.transicion_estado_ot AS t ON t.id_estado_origen = c.id_estado_anterior AND t.id_estado_destino = c.id_estado_ot
        LEFT JOIN operativo.motivo_estado_ot AS m ON m.id_motivo_es = c.id_motivo_es
        WHERE ea.fase = 'en_curso' AND en.fase = 'en_curso' AND COALESCE(t.exige_motivo, FALSE)
        GROUP BY m.codigo, m.descripcion
        ORDER BY 4 DESC, 3 DESC
        """;

    private const string ColumnasDeKpis = """
        count(*)::int AS "Creadas",
        (count(*) FILTER (WHERE o.tipo))::int AS "Preventivas",
        (count(*) FILTER (WHERE NOT o.tipo))::int AS "Correctivas",
        (count(*) FILTER (WHERE o.tipo AND o.fecha_cierre IS NOT NULL))::int AS "PreventivasCerradas",
        (count(*) FILTER (WHERE o.tipo AND o.fecha_cierre IS NOT NULL
                            AND (o.fecha_cierre AT TIME ZONE 'UTC')::date <= o.fecha_prevista_ejecucion))::int AS "PreventivasEnPlazo",
        (count(*) FILTER (WHERE NOT o.tipo AND o.fecha_cierre IS NOT NULL))::int AS "CorrectivasCerradas",
        COALESCE((sum(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0)
            FILTER (WHERE NOT o.tipo AND o.fecha_cierre IS NOT NULL))::float8, 0) AS "HorasParada",
        COALESCE((sum(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0)
            FILTER (WHERE o.tipo AND o.fecha_cierre IS NOT NULL))::float8, 0) AS "HorasPreventivas"
        """;

    private const string SqlKpis = $"""
        SELECT {ColumnasDeKpis}
        FROM operativo.orden_trabajo AS o
        WHERE TRUE {Filtros}
        """;

    private const string SqlKpisPorMes = $"""
        SELECT date_trunc('month', o.fecha_creada AT TIME ZONE 'UTC') AS "Periodo", {ColumnasDeKpis}
        FROM operativo.orden_trabajo AS o
        WHERE TRUE {Filtros}
        GROUP BY 1
        ORDER BY 1
        """;

    // Agrupa en PostgreSQL: nunca carga órdenes individuales ni multiplica su duración por el historial de pausas.
    private const string SqlPlanificacion = $"""
        SELECT date_trunc('month', o.fecha_creada AT TIME ZONE 'UTC') AS "Periodo",
               tm.id_tipo_maquina AS "TipoMaquinaId", tm.nombre AS "TipoMaquina",
               count(*)::int AS "OrdenesCerradas",
               COALESCE(sum(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0)
                   FILTER (WHERE o.tipo), 0)::float8 AS "HorasPreventivas",
               COALESCE(sum(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0)
                   FILTER (WHERE NOT o.tipo), 0)::float8 AS "HorasCorrectivas"
        FROM operativo.orden_trabajo AS o
        JOIN operativo.activo AS a ON a.id_activo = o.id_activo
        JOIN operativo.modelo_maquina AS mm ON mm.id_modelo_maquina = a.id_modelo_maquina
        JOIN operativo.tipo_maquina AS tm ON tm.id_tipo_maquina = mm.id_tipo_maquina
        WHERE o.fecha_cierre IS NOT NULL {Filtros}
        GROUP BY 1, 2, 3
        ORDER BY 1, 2
        """;

    private const string SqlEnvejecimiento = $"""
        SELECT (count(*) FILTER (WHERE edad <= 3))::int AS "HastaTresDias",
               (count(*) FILTER (WHERE edad > 3 AND edad <= 7))::int AS "CuatroASiete",
               (count(*) FILTER (WHERE edad > 7 AND edad <= 30))::int AS "OchoATreinta",
               (count(*) FILTER (WHERE edad > 30 AND edad <= 90))::int AS "TreintaYUnoANoventa",
               (count(*) FILTER (WHERE edad > 90))::int AS "MasDeNoventa",
               count(*)::int AS "Total"
        FROM (
            SELECT EXTRACT(EPOCH FROM (now() - o.fecha_creada)) / 86400.0 AS edad
            FROM operativo.orden_trabajo AS o
            {FaseActual}
            WHERE actual.fase IN ('pendiente', 'en_curso', 'por_validar')
              AND (@clienteId IS NULL OR o.id_cliente = @clienteId)
              AND (@activoId IS NULL OR o.id_activo = @activoId)
        ) AS edades
        """;

    private const string SqlMasAntiguas = $"""
        SELECT o.id_orden AS "OrdenId", COALESCE(a.nombre, a.num_serie) AS "NombreActivo", cl.razon_social AS "Cliente",
               actual.nombre AS "Estado", (EXTRACT(EPOCH FROM (now() - o.fecha_creada)) / 86400.0)::float8 AS "EdadDias"
        FROM operativo.orden_trabajo AS o
        {FaseActual}
        JOIN operativo.activo AS a ON a.id_activo = o.id_activo
        JOIN operativo.cliente AS cl ON cl.id_cliente = o.id_cliente
        WHERE actual.fase IN ('pendiente', 'en_curso', 'por_validar')
          AND (@clienteId IS NULL OR o.id_cliente = @clienteId)
          AND (@activoId IS NULL OR o.id_activo = @activoId)
        ORDER BY o.fecha_creada, o.id_orden
        LIMIT @limite
        """;

    private const string SqlCierresRecientes = """
        SELECT count(*)::int AS "Value"
        FROM operativo.orden_trabajo AS o
        WHERE o.fecha_cierre >= now() - make_interval(days => @dias)
          AND (@clienteId IS NULL OR o.id_cliente = @clienteId)
          AND (@activoId IS NULL OR o.id_activo = @activoId)
        """;

    private const string SqlEstimacion = $"""
        SELECT count(*)::int AS "Ordenes",
               avg(o.tiempo_estimado_horas)::float8 AS "HorasEstimadas",
               avg(real.horas)::float8 AS "HorasReales",
               avg((real.horas - o.tiempo_estimado_horas) / o.tiempo_estimado_horas * 100.0)::float8 AS "DesviacionPromedioPorcentaje"
        FROM operativo.orden_trabajo AS o
        JOIN LATERAL (
            SELECT EXTRACT(EPOCH FROM (min(h.fecha_hora) - o.fecha_inicio)) / 3600.0 AS horas
            FROM operativo.historial_estado_ot AS h
            JOIN operativo.estado_ot AS e ON e.id_estado_ot = h.id_estado_ot
            WHERE h.id_orden = o.id_orden AND e.fase = 'por_validar'
        ) AS real ON TRUE
        WHERE o.tiempo_estimado_horas IS NOT NULL AND o.fecha_inicio IS NOT NULL AND real.horas IS NOT NULL AND real.horas >= 0 {Filtros}
        """;

    private const string SqlIntervalos = $"""
        WITH correctivas AS (
            SELECT o.id_activo, o.fecha_creada, o.lectura_uso,
                   LAG(o.fecha_creada) OVER (PARTITION BY o.id_activo ORDER BY o.fecha_creada, o.id_orden) AS anterior,
                   LAG(o.lectura_uso) OVER (PARTITION BY o.id_activo ORDER BY o.fecha_creada, o.id_orden) AS lectura_anterior
            FROM operativo.orden_trabajo AS o
            WHERE NOT o.tipo {Filtros}
        )
        SELECT a.id_activo AS "ActivoId", COALESCE(a.nombre, a.num_serie) AS "NombreActivo", cl.razon_social AS "Cliente",
               mm.nombre AS "Modelo", a.unidad_uso AS "UnidadUso",
               (row_number() OVER (PARTITION BY a.id_activo ORDER BY c.fecha_creada))::int AS "Numero",
               (EXTRACT(EPOCH FROM (c.fecha_creada - c.anterior)) / 86400.0)::float8 AS "Dias",
               CASE WHEN c.lectura_uso IS NOT NULL AND c.lectura_anterior IS NOT NULL
                    THEN (c.lectura_uso - c.lectura_anterior)::float8 END AS "UsoEntreFallas"
        FROM correctivas AS c
        JOIN operativo.activo AS a ON a.id_activo = c.id_activo
        JOIN operativo.cliente AS cl ON cl.id_cliente = a.id_cliente
        JOIN operativo.modelo_maquina AS mm ON mm.id_modelo_maquina = a.id_modelo_maquina
        WHERE c.anterior IS NOT NULL
        ORDER BY a.id_activo, c.fecha_creada
        """;

    // Reglas de aviso: cada consulta responde solo con el conteo que la regla necesita.

    private const string SqlReincidenciaPorActivo = """
        SELECT a.id_activo AS "ActivoId", COALESCE(a.nombre, a.num_serie) AS "NombreActivo", cl.razon_social AS "Cliente",
               (count(*) FILTER (WHERE o.fecha_creada >= now() - make_interval(days => @ventana)))::int AS "EnVentana",
               (count(*) FILTER (WHERE o.fecha_creada >= now() - make_interval(days => @ventanaCritica)))::int AS "EnVentanaCritica"
        FROM operativo.orden_trabajo AS o
        JOIN operativo.activo AS a ON a.id_activo = o.id_activo
        JOIN operativo.cliente AS cl ON cl.id_cliente = a.id_cliente
        WHERE NOT o.tipo AND o.fecha_creada >= now() - make_interval(days => GREATEST(@ventana, @ventanaCritica))
        GROUP BY a.id_activo, a.nombre, a.num_serie, cl.razon_social
        ORDER BY 5 DESC, 4 DESC, a.id_activo
        """;

    private const string SqlReactivoPorTipo = """
        SELECT tm.id_tipo_maquina AS "Id", tm.nombre AS "Nombre",
               (count(*) FILTER (WHERE NOT o.tipo))::int AS "Parte", count(*)::int AS "Total"
        FROM operativo.orden_trabajo AS o
        JOIN operativo.activo AS a ON a.id_activo = o.id_activo
        JOIN operativo.modelo_maquina AS mm ON mm.id_modelo_maquina = a.id_modelo_maquina
        JOIN operativo.tipo_maquina AS tm ON tm.id_tipo_maquina = mm.id_tipo_maquina
        WHERE o.fecha_creada >= now() - make_interval(days => @ventana)
        GROUP BY tm.id_tipo_maquina, tm.nombre
        ORDER BY tm.nombre
        """;

    private const string SqlPreventivosFueraDePlazo = """
        SELECT 0::bigint AS "Id", 'Preventivos'::text AS "Nombre",
               (count(*) FILTER (WHERE (o.fecha_cierre AT TIME ZONE 'UTC')::date <= o.fecha_prevista_ejecucion))::int AS "Parte",
               count(*)::int AS "Total"
        FROM operativo.orden_trabajo AS o
        WHERE o.tipo AND o.fecha_cierre IS NOT NULL AND o.fecha_cierre >= now() - make_interval(days => @ventana)
        """;

    private const string SqlAbiertasMasViejasQue = $"""
        SELECT (count(*) FILTER (WHERE edad > @umbral))::int AS "Cantidad",
               (count(*) FILTER (WHERE edad > @umbralCritico))::int AS "Criticas"
        FROM (
            SELECT EXTRACT(EPOCH FROM (now() - o.fecha_creada)) / 86400.0 AS edad
            FROM operativo.orden_trabajo AS o
            {FaseActual}
            WHERE actual.fase IN ('pendiente', 'en_curso', 'por_validar')
        ) AS edades
        """;

    private const string SqlSinMovimiento = $"""
        SELECT (count(*) FILTER (WHERE dias > @umbral))::int AS "Cantidad",
               (count(*) FILTER (WHERE dias > @umbralCritico))::int AS "Criticas"
        FROM (
            SELECT EXTRACT(EPOCH FROM (now() - actual.fecha_hora)) / 86400.0 AS dias
            FROM operativo.orden_trabajo AS o
            {FaseActual}
            WHERE actual.fase = ANY (string_to_array(@fases, ','))
        ) AS esperas
        """;

    private const string SqlCriticasSinAsignar = $"""
        SELECT (count(*) FILTER (WHERE horas > @umbral))::int AS "Cantidad",
               (count(*) FILTER (WHERE horas > @umbralCritico))::int AS "Criticas"
        FROM (
            SELECT EXTRACT(EPOCH FROM (now() - o.fecha_creada)) / 3600.0 AS horas
            FROM operativo.orden_trabajo AS o
            {FaseActual}
            WHERE actual.fase IN ('pendiente', 'en_curso') AND o.id_usuario_tecnico IS NULL AND o.prioridad >= 5
        ) AS esperas
        """;

    private const string SqlTiempoDeReparacionPorTipo = """
        SELECT tm.id_tipo_maquina AS "TipoMaquinaId", tm.nombre AS "TipoMaquina",
               (avg(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0)
                   FILTER (WHERE o.fecha_cierre >= now() - make_interval(days => @ventana)))::float8 AS "PromedioReciente",
               (count(*) FILTER (WHERE o.fecha_cierre >= now() - make_interval(days => @ventana)))::int AS "MuestraReciente",
               (avg(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0)
                   FILTER (WHERE o.fecha_cierre < now() - make_interval(days => @ventana)))::float8 AS "PromedioHistorico",
               (count(*) FILTER (WHERE o.fecha_cierre < now() - make_interval(days => @ventana)))::int AS "MuestraHistorica"
        FROM operativo.orden_trabajo AS o
        JOIN operativo.activo AS a ON a.id_activo = o.id_activo
        JOIN operativo.modelo_maquina AS mm ON mm.id_modelo_maquina = a.id_modelo_maquina
        JOIN operativo.tipo_maquina AS tm ON tm.id_tipo_maquina = mm.id_tipo_maquina
        WHERE NOT o.tipo AND o.fecha_cierre IS NOT NULL
        GROUP BY tm.id_tipo_maquina, tm.nombre
        ORDER BY tm.nombre
        """;

    private const string SqlParadaPorActivo = """
        SELECT o.id_activo AS "ActivoId",
               COALESCE(sum(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0), 0)::float8 AS "Horas"
        FROM operativo.orden_trabajo AS o
        WHERE NOT o.tipo AND o.fecha_cierre IS NOT NULL AND o.fecha_cierre >= now() - make_interval(days => @ventana)
        GROUP BY o.id_activo
        ORDER BY 2 DESC, 1
        """;

    public async Task<List<CierrePeriodoFila>> ObtenerCierresPorPeriodoAsync(
        string unidad, FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<CierrePeriodoFila>(SqlCierres, filtro, new NpgsqlParameter("unidad", unidad)).ToListAsync(cancelacion);

    public async Task<List<CumplimientoPorTipoFila>> ObtenerCumplimientoPorTipoAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<CumplimientoPorTipoFila>(SqlCumplimientoPorTipo, filtro).ToListAsync(cancelacion);

    public async Task<List<PausaPorMotivoFila>> ObtenerPausasPorMotivoAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<PausaPorMotivoFila>(SqlPausasPorMotivo, filtro).ToListAsync(cancelacion);

    public async Task<KpisFila> ObtenerKpisAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<KpisFila>(SqlKpis, filtro).SingleAsync(cancelacion);

    public async Task<List<KpisPorMesFila>> ObtenerKpisPorMesAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<KpisPorMesFila>(SqlKpisPorMes, filtro).ToListAsync(cancelacion);

    public async Task<List<PlanificacionFila>> ObtenerPlanificacionAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<PlanificacionFila>(SqlPlanificacion, filtro).ToListAsync(cancelacion);

    public async Task<EnvejecimientoFila> ObtenerEnvejecimientoAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<EnvejecimientoFila>(SqlEnvejecimiento, filtro).SingleAsync(cancelacion);

    public async Task<List<OrdenAntiguaFila>> ObtenerMasAntiguasAsync(int limite, FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<OrdenAntiguaFila>(SqlMasAntiguas, filtro, new NpgsqlParameter("limite", limite)).ToListAsync(cancelacion);

    public async Task<int> ContarCierresRecientesAsync(int dias, FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<int>(SqlCierresRecientes, filtro, new NpgsqlParameter("dias", dias)).SingleAsync(cancelacion);

    public async Task<EstimacionFila> ObtenerEstimacionAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<EstimacionFila>(SqlEstimacion, filtro).SingleAsync(cancelacion);

    public async Task<List<IntervaloFila>> ObtenerIntervalosEntreCorrectivasAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<IntervaloFila>(SqlIntervalos, filtro).ToListAsync(cancelacion);

    public async Task<List<ConteoPorActivoFila>> ContarCorrectivasPorActivoAsync(
        int ventana, int ventanaCritica, CancellationToken cancelacion) =>
        await contexto.Database.SqlQueryRaw<ConteoPorActivoFila>(SqlReincidenciaPorActivo,
            new NpgsqlParameter("ventana", ventana), new NpgsqlParameter("ventanaCritica", ventanaCritica)).ToListAsync(cancelacion);

    public async Task<List<ProporcionFila>> ObtenerProporcionDeCorrectivasPorTipoAsync(int ventana, CancellationToken cancelacion) =>
        await contexto.Database.SqlQueryRaw<ProporcionFila>(SqlReactivoPorTipo, new NpgsqlParameter("ventana", ventana)).ToListAsync(cancelacion);

    public async Task<ProporcionFila> ObtenerPreventivosEnPlazoAsync(int ventana, CancellationToken cancelacion) =>
        await contexto.Database.SqlQueryRaw<ProporcionFila>(SqlPreventivosFueraDePlazo, new NpgsqlParameter("ventana", ventana)).SingleAsync(cancelacion);

    public async Task<CantidadFila> ContarAbiertasMasViejasQueAsync(int dias, int diasCritico, CancellationToken cancelacion) =>
        await contexto.Database.SqlQueryRaw<CantidadFila>(SqlAbiertasMasViejasQue,
            new NpgsqlParameter("umbral", (double)dias), new NpgsqlParameter("umbralCritico", (double)diasCritico)).SingleAsync(cancelacion);

    public async Task<CantidadFila> ContarSinMovimientoAsync(string fases, int dias, int diasCritico, CancellationToken cancelacion) =>
        await contexto.Database.SqlQueryRaw<CantidadFila>(SqlSinMovimiento, new NpgsqlParameter("fases", fases),
            new NpgsqlParameter("umbral", (double)dias), new NpgsqlParameter("umbralCritico", (double)diasCritico)).SingleAsync(cancelacion);

    public async Task<CantidadFila> ContarCriticasSinAsignarAsync(int horas, int horasCritico, CancellationToken cancelacion) =>
        await contexto.Database.SqlQueryRaw<CantidadFila>(SqlCriticasSinAsignar,
            new NpgsqlParameter("umbral", (double)horas), new NpgsqlParameter("umbralCritico", (double)horasCritico)).SingleAsync(cancelacion);

    public async Task<List<TiempoReparacionFila>> ObtenerTiempoDeReparacionPorTipoAsync(int ventana, CancellationToken cancelacion) =>
        await contexto.Database.SqlQueryRaw<TiempoReparacionFila>(SqlTiempoDeReparacionPorTipo, new NpgsqlParameter("ventana", ventana)).ToListAsync(cancelacion);

    public async Task<List<ParadaActivoFila>> ObtenerParadaPorActivoAsync(int ventana, CancellationToken cancelacion) =>
        await contexto.Database.SqlQueryRaw<ParadaActivoFila>(SqlParadaPorActivo, new NpgsqlParameter("ventana", ventana)).ToListAsync(cancelacion);

    private IQueryable<T> Consultar<T>(string sql, FiltroAnalitica filtro, params NpgsqlParameter[] extra)
    {
        var parametros = AnaliticaDAO.ParametrosDelFiltro(filtro);
        parametros.AddRange(extra);
        return contexto.Database.SqlQueryRaw<T>(sql, parametros.ToArray<object>());
    }
}
