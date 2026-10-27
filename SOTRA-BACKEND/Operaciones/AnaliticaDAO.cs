using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SOTRA_BACKEND.Contexto;

namespace SOTRA_BACKEND.Operaciones;

public sealed record FiltroAnalitica(DateOnly? Desde, DateOnly? Hasta, long? ClienteId, long? ActivoId);

public sealed class PuntoTendenciaFila
{
    public DateTime Periodo { get; set; }
    public int Preventivas { get; set; }
    public int Correctivas { get; set; }
}

public sealed class DuracionFila
{
    public int Cantidad { get; set; }
    public double? Promedio { get; set; }
    public double? Mediana { get; set; }
    public double? Percentil90 { get; set; }
}

public sealed class CumplimientoFila
{
    public int Evaluadas { get; set; }
    public int EnPlazo { get; set; }
    public double? AtrasoPromedioDias { get; set; }
}

public sealed class RepuestoUsadoFila
{
    public long RepuestoId { get; set; }
    public string Codigo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public int Ordenes { get; set; }
}

public sealed class EtapaFila
{
    public string Estado { get; set; } = "";
    public int Cantidad { get; set; }
    public double PromedioHoras { get; set; }
}

public sealed class ReincidenciaFila
{
    public long ActivoId { get; set; }
    public string NombreActivo { get; set; } = "";
    public string Cliente { get; set; } = "";
    public int Correctivas { get; set; }
    public int Reincidencias { get; set; }
    public double? DiasPromedioEntreIntervenciones { get; set; }
    public DateTime UltimaIntervencion { get; set; }
}

public sealed class OrdenAbiertaFila
{
    public long OrdenId { get; set; }
    public long ActivoId { get; set; }
    public string NombreActivo { get; set; } = "";
    public string Cliente { get; set; } = "";
    public string Estado { get; set; } = "";
    public string? CodigoAlerta { get; set; }
    public short Prioridad { get; set; }
    public double HorasEnEstado { get; set; }
    public double HorasDesdeCreacion { get; set; }
}

public sealed class ResumenDeOrdenesFila
{
    public int Creadas { get; set; }
    public int Cerradas { get; set; }
    public int Eliminadas { get; set; }
    public int Abiertas { get; set; }
    public int Preventivas { get; set; }
    public int Correctivas { get; set; }
}

public sealed class CambiosDeProcesoFila
{
    public int Cantidad { get; set; }
    public int Ordenes { get; set; }
    public double? PromedioHoras { get; set; }
}

public sealed class MotivoFila
{
    public string Texto { get; set; } = "";
    public int Cantidad { get; set; }
}

public sealed class ReasignacionesFila
{
    public int Cantidad { get; set; }
    public int Ordenes { get; set; }
}

public sealed class MaquinariaFila
{
    public long Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Cliente { get; set; }
    public int Ordenes { get; set; }
    public int Correctivas { get; set; }
    public int Preventivas { get; set; }
    public double? HorasPromedioEjecucion { get; set; }
    public double HorasParada { get; set; }
}

/// <summary>Consultas agregadas del dashboard: se resuelven en la base con GROUP BY y funciones de ventana.</summary>
public class AnaliticaDAO
{
    /// <summary>Filtros opcionales de las órdenes (alias <c>o</c>): cada uno se ignora si su parámetro es nulo.</summary>
    private const string FiltrosDeOrden = """
        AND (@desde IS NULL OR o.fecha_creada >= @desde)
        AND (@hasta IS NULL OR o.fecha_creada < @hasta)
        AND (@clienteId IS NULL OR o.id_cliente = @clienteId)
        AND (@activoId IS NULL OR o.id_activo = @activoId)
        """;

    private const string SqlTendencias = $"""
        SELECT date_trunc(@unidad, o.fecha_creada AT TIME ZONE 'UTC') AS "Periodo",
               (count(*) FILTER (WHERE o.tipo))::int AS "Preventivas",
               (count(*) FILTER (WHERE NOT o.tipo))::int AS "Correctivas"
        FROM operativo.orden_trabajo AS o
        WHERE TRUE {FiltrosDeOrden}
        GROUP BY 1
        ORDER BY 1
        """;

    private const string SqlContarOrdenes = $"""
        SELECT count(*)::int AS "Value"
        FROM operativo.orden_trabajo AS o
        WHERE TRUE {FiltrosDeOrden}
        """;

    private const string SqlDuracion = $"""
        SELECT count(*)::int AS "Cantidad",
               avg(horas)::float8 AS "Promedio",
               (percentile_cont(0.5) WITHIN GROUP (ORDER BY horas))::float8 AS "Mediana",
               (percentile_cont(0.9) WITHIN GROUP (ORDER BY horas))::float8 AS "Percentil90"
        FROM (
            SELECT EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0 AS horas
            FROM operativo.orden_trabajo AS o
            WHERE o.fecha_cierre IS NOT NULL {FiltrosDeOrden}
        ) AS duraciones
        """;

    private const string SqlEtapas = $"""
        SELECT e.nombre AS "Estado",
               count(*)::int AS "Cantidad",
               avg(EXTRACT(EPOCH FROM (tramos.siguiente - tramos.fecha_hora)) / 3600.0)::float8 AS "PromedioHoras"
        FROM (
            SELECT h.id_estado_ot, h.fecha_hora,
                   LEAD(h.fecha_hora) OVER (PARTITION BY h.id_orden ORDER BY h.fecha_hora, h.id_h_estado_ot) AS siguiente
            FROM operativo.historial_estado_ot AS h
            JOIN operativo.orden_trabajo AS o ON o.id_orden = h.id_orden
            WHERE TRUE {FiltrosDeOrden}
        ) AS tramos
        JOIN operativo.estado_ot AS e ON e.id_estado_ot = tramos.id_estado_ot
        WHERE tramos.siguiente IS NOT NULL
        GROUP BY e.nombre
        ORDER BY avg(EXTRACT(EPOCH FROM (tramos.siguiente - tramos.fecha_hora))) DESC
        """;

    private const string SqlReincidencias = $"""
        WITH correctivas AS (
            SELECT o.id_activo, o.fecha_creada,
                   LAG(COALESCE(o.fecha_cierre, o.fecha_creada))
                       OVER (PARTITION BY o.id_activo ORDER BY o.fecha_creada, o.id_orden) AS anterior
            FROM operativo.orden_trabajo AS o
            WHERE NOT o.tipo {FiltrosDeOrden}
        )
        SELECT a.id_activo AS "ActivoId",
               COALESCE(a.nombre, a.num_serie) AS "NombreActivo",
               cl.razon_social AS "Cliente",
               count(*)::int AS "Correctivas",
               (count(*) FILTER (WHERE c.anterior IS NOT NULL
                                   AND c.fecha_creada - c.anterior <= make_interval(days => @dias)))::int AS "Reincidencias",
               (avg(EXTRACT(EPOCH FROM (c.fecha_creada - c.anterior)) / 86400.0)
                    FILTER (WHERE c.anterior IS NOT NULL))::float8 AS "DiasPromedioEntreIntervenciones",
               max(c.fecha_creada) AS "UltimaIntervencion"
        FROM correctivas AS c
        JOIN operativo.activo AS a ON a.id_activo = c.id_activo
        JOIN operativo.cliente AS cl ON cl.id_cliente = a.id_cliente
        GROUP BY a.id_activo, a.nombre, a.num_serie, cl.razon_social
        HAVING count(*) FILTER (WHERE c.anterior IS NOT NULL
                                  AND c.fecha_creada - c.anterior <= make_interval(days => @dias)) >= @minimo
        ORDER BY 5 DESC, 4 DESC, a.id_activo
        LIMIT @limite
        """;

    private const string SqlOrdenesAbiertas = $"""
        SELECT o.id_orden AS "OrdenId",
               o.id_activo AS "ActivoId",
               COALESCE(a.nombre, a.num_serie) AS "NombreActivo",
               cl.razon_social AS "Cliente",
               e.nombre AS "Estado",
               CASE WHEN e.codigo_alerta = 'demorada_para_iniciar' AND o.id_usuario_tecnico IS NULL
                    THEN 'sin_asignar' ELSE e.codigo_alerta END AS "CodigoAlerta",
               o.prioridad AS "Prioridad",
               GREATEST(0, EXTRACT(EPOCH FROM (now() - CASE WHEN e.codigo_alerta = 'demorada_para_iniciar'
                    THEN GREATEST(actual.fecha_hora, COALESCE(asignacion.fecha_hora, actual.fecha_hora))
                    ELSE actual.fecha_hora END)) / 3600.0)::float8 AS "HorasEnEstado",
               GREATEST(0, EXTRACT(EPOCH FROM (now() - o.fecha_creada)) / 3600.0)::float8 AS "HorasDesdeCreacion"
        FROM operativo.orden_trabajo AS o
        JOIN LATERAL (
            SELECT h.id_estado_ot, h.fecha_hora
            FROM operativo.historial_estado_ot AS h
            WHERE h.id_orden = o.id_orden
            ORDER BY h.fecha_hora DESC, h.id_h_estado_ot DESC
            LIMIT 1
        ) AS actual ON TRUE
        LEFT JOIN LATERAL (
            SELECT max(t.fecha_hora) AS fecha_hora
            FROM operativo.historial_tecnico_ot AS t
            WHERE t.id_orden = o.id_orden
        ) AS asignacion ON TRUE
        JOIN operativo.estado_ot AS e ON e.id_estado_ot = actual.id_estado_ot
        JOIN operativo.activo AS a ON a.id_activo = o.id_activo
        JOIN operativo.cliente AS cl ON cl.id_cliente = o.id_cliente
        WHERE e.fase IN ('{FasesDeEstado.Pendiente}', '{FasesDeEstado.EnCurso}') {FiltrosDeOrden}
        """;

    /// <summary>
    /// La fecha prevista de ejecución se guarda como texto «aaaa-mm-dd» en la observación del primer estado de la orden;
    /// solo se evalúan las órdenes cerradas con una fecha válida.
    /// </summary>
    private const string SqlCumplimiento = $"""
        SELECT count(*)::int AS "Evaluadas",
               (count(*) FILTER (WHERE (o.fecha_cierre AT TIME ZONE 'UTC')::date <= prevista.fecha))::int AS "EnPlazo",
               (avg((o.fecha_cierre AT TIME ZONE 'UTC')::date - prevista.fecha)
                    FILTER (WHERE (o.fecha_cierre AT TIME ZONE 'UTC')::date > prevista.fecha))::float8 AS "AtrasoPromedioDias"
        FROM operativo.orden_trabajo AS o
        JOIN LATERAL (
            SELECT to_date(h.observacion, 'YYYY-MM-DD') AS fecha
            FROM operativo.historial_estado_ot AS h
            WHERE h.id_orden = o.id_orden AND h.observacion ~ '^[0-9][0-9][0-9][0-9]-[0-9][0-9]-[0-9][0-9]$'
            ORDER BY h.fecha_hora, h.id_h_estado_ot
            LIMIT 1
        ) AS prevista ON TRUE
        WHERE o.fecha_cierre IS NOT NULL {FiltrosDeOrden}
        """;

    private const string SqlRepuestosUsados = $"""
        SELECT r.id_repuesto AS "RepuestoId",
               r.codigo AS "Codigo",
               r.nombre AS "Nombre",
               count(DISTINCT o.id_orden)::int AS "Ordenes"
        FROM operativo.ot_repuesto AS otr
        JOIN operativo.orden_trabajo AS o ON o.id_orden = otr.id_orden
        JOIN operativo.repuesto AS r ON r.id_repuesto = otr.id_repuesto
        WHERE TRUE {FiltrosDeOrden}
        GROUP BY r.id_repuesto, r.codigo, r.nombre
        ORDER BY 4 DESC, r.id_repuesto
        LIMIT @limite
        """;

    private const string ConsultaPrimeraRevision = """
        (SELECT min(h.fecha_hora)
         FROM operativo.historial_estado_ot AS h
         JOIN operativo.estado_ot AS e ON e.id_estado_ot = h.id_estado_ot
         WHERE h.id_orden = o.id_orden AND e.fase = 'por_validar')
        """;

    private const string SqlResumenDeOrdenes = $"""
        SELECT count(*)::int AS "Creadas",
               (count(*) FILTER (WHERE o.fecha_cierre IS NOT NULL))::int AS "Cerradas",
               (count(*) FILTER (WHERE actual.fase = 'eliminada'))::int AS "Eliminadas",
               (count(*) FILTER (WHERE actual.fase IN ('pendiente', 'en_curso', 'por_validar')))::int AS "Abiertas",
               (count(*) FILTER (WHERE o.tipo))::int AS "Preventivas",
               (count(*) FILTER (WHERE NOT o.tipo))::int AS "Correctivas"
        FROM operativo.orden_trabajo AS o
        LEFT JOIN LATERAL (
            SELECT e.fase
            FROM operativo.historial_estado_ot AS h
            JOIN operativo.estado_ot AS e ON e.id_estado_ot = h.id_estado_ot
            WHERE h.id_orden = o.id_orden
            ORDER BY h.fecha_hora DESC, h.id_h_estado_ot DESC
            LIMIT 1
        ) AS actual ON TRUE
        WHERE TRUE {FiltrosDeOrden}
        """;

    /// <summary>Horas entre dos fechas de la orden (<c>fecha_creada</c>, <c>fecha_inicio</c>, <c>fecha_revision</c> o <c>fecha_cierre</c>); son constantes del código, nunca texto de la solicitud.</summary>
    private static string SqlHorasEntre(string desde, string hasta) => $"""
        SELECT count(*)::int AS "Cantidad",
               avg(horas)::float8 AS "Promedio",
               (percentile_cont(0.5) WITHIN GROUP (ORDER BY horas))::float8 AS "Mediana",
               (percentile_cont(0.9) WITHIN GROUP (ORDER BY horas))::float8 AS "Percentil90"
        FROM (
            SELECT EXTRACT(EPOCH FROM (b.{hasta} - b.{desde})) / 3600.0 AS horas
            FROM (
                SELECT o.fecha_creada, o.fecha_inicio, o.fecha_cierre, {ConsultaPrimeraRevision} AS fecha_revision
                FROM operativo.orden_trabajo AS o
                WHERE TRUE {FiltrosDeOrden}
            ) AS b
            WHERE b.{desde} IS NOT NULL AND b.{hasta} IS NOT NULL AND b.{hasta} >= b.{desde}
        ) AS duraciones
        """;

    private static readonly string SqlRespuesta = SqlHorasEntre("fecha_creada", "fecha_inicio");
    private static readonly string SqlEjecucion = SqlHorasEntre("fecha_inicio", "fecha_revision");
    private static readonly string SqlRevision = SqlHorasEntre("fecha_revision", "fecha_cierre");

    /// <summary>
    /// Cambios de estado de un tipo de proceso. Una pausa es un cambio dentro de la fase en curso que la base marca como
    /// «exige motivo»; una devolución es el paso de la fase por validar a la pendiente. Se identifican por fase y por las
    /// reglas de la base, no por el nombre del estado.
    /// </summary>
    private static string SqlCambios(string condicion, string columnas, string final) => $"""
        WITH cambios AS (
            SELECT h.id_orden, h.fecha_hora, h.observacion, h.id_estado_anterior, h.id_estado_ot,
                   LEAD(h.fecha_hora) OVER (PARTITION BY h.id_orden ORDER BY h.fecha_hora, h.id_h_estado_ot) AS siguiente
            FROM operativo.historial_estado_ot AS h
            JOIN operativo.orden_trabajo AS o ON o.id_orden = h.id_orden
            WHERE TRUE {FiltrosDeOrden}
        ), elegidos AS (
            SELECT c.id_orden, c.observacion, EXTRACT(EPOCH FROM (c.siguiente - c.fecha_hora)) / 3600.0 AS horas
            FROM cambios AS c
            JOIN operativo.estado_ot AS ea ON ea.id_estado_ot = c.id_estado_anterior
            JOIN operativo.estado_ot AS en ON en.id_estado_ot = c.id_estado_ot
            LEFT JOIN operativo.transicion_estado_ot AS t ON t.id_estado_origen = c.id_estado_anterior AND t.id_estado_destino = c.id_estado_ot
            WHERE {condicion}
        )
        SELECT {columnas} FROM elegidos {final}
        """;

    private const string CondicionPausa = "ea.fase = 'en_curso' AND en.fase = 'en_curso' AND COALESCE(t.exige_motivo, FALSE)";
    private const string CondicionDevolucion = "ea.fase = 'por_validar' AND en.fase = 'pendiente'";

    private static readonly string SqlTotalPausas = SqlCambios(CondicionPausa,
        """count(*)::int AS "Cantidad", count(DISTINCT id_orden)::int AS "Ordenes", avg(horas)::float8 AS "PromedioHoras" """, "");
    private static readonly string SqlTotalDevoluciones = SqlCambios(CondicionDevolucion,
        """count(*)::int AS "Cantidad", count(DISTINCT id_orden)::int AS "Ordenes", NULL::float8 AS "PromedioHoras" """, "");
    private static readonly string SqlMotivosDePausa = SqlCambios(CondicionPausa,
        """COALESCE(min(btrim(observacion)), '') AS "Texto", count(*)::int AS "Cantidad" """,
        "GROUP BY lower(btrim(COALESCE(observacion, ''))) ORDER BY 2 DESC, 1 LIMIT @limite");
    private static readonly string SqlMotivosDeDevolucion = SqlCambios(CondicionDevolucion,
        """COALESCE(min(btrim(observacion)), '') AS "Texto", count(*)::int AS "Cantidad" """,
        "GROUP BY lower(btrim(COALESCE(observacion, ''))) ORDER BY 2 DESC, 1 LIMIT @limite");

    private const string SqlReasignaciones = $"""
        SELECT count(*)::int AS "Cantidad", count(DISTINCT t.id_orden)::int AS "Ordenes"
        FROM operativo.historial_tecnico_ot AS t
        JOIN operativo.orden_trabajo AS o ON o.id_orden = t.id_orden
        WHERE t.id_tecnico_anterior IS NOT NULL AND t.id_tecnico_nuevo IS NOT NULL {FiltrosDeOrden}
        """;

    /// <summary>
    /// Por máquina (tipo o activo): órdenes, correctivas y preventivas, horas promedio de ejecución de las correctivas
    /// (de inicio a revisión) y horas de parada acumuladas (de la creación al cierre de las correctivas cerradas).
    /// </summary>
    private static string SqlMaquinaria(string idNombre, string agrupar, string orden, string limite) => $"""
        SELECT {idNombre},
               count(*)::int AS "Ordenes",
               (count(*) FILTER (WHERE NOT o.tipo))::int AS "Correctivas",
               (count(*) FILTER (WHERE o.tipo))::int AS "Preventivas",
               (avg(EXTRACT(EPOCH FROM (rev.fecha - o.fecha_inicio)) / 3600.0)
                   FILTER (WHERE NOT o.tipo AND o.fecha_inicio IS NOT NULL AND rev.fecha IS NOT NULL AND rev.fecha >= o.fecha_inicio))::float8 AS "HorasPromedioEjecucion",
               COALESCE((sum(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0)
                   FILTER (WHERE NOT o.tipo AND o.fecha_cierre IS NOT NULL))::float8, 0) AS "HorasParada"
        FROM operativo.orden_trabajo AS o
        JOIN operativo.activo AS a ON a.id_activo = o.id_activo
        JOIN operativo.cliente AS cl ON cl.id_cliente = a.id_cliente
        JOIN operativo.modelo_maquina AS mm ON mm.id_modelo_maquina = a.id_modelo_maquina
        JOIN operativo.tipo_maquina AS tm ON tm.id_tipo_maquina = mm.id_tipo_maquina
        LEFT JOIN LATERAL (
            SELECT min(h.fecha_hora) AS fecha
            FROM operativo.historial_estado_ot AS h
            JOIN operativo.estado_ot AS e ON e.id_estado_ot = h.id_estado_ot
            WHERE h.id_orden = o.id_orden AND e.fase = 'por_validar'
        ) AS rev ON TRUE
        WHERE TRUE {FiltrosDeOrden}
        GROUP BY {agrupar}
        ORDER BY {orden}
        {limite}
        """;

    private static readonly string SqlMaquinariaPorTipo = SqlMaquinaria(
        """tm.id_tipo_maquina AS "Id", tm.nombre AS "Nombre", NULL::text AS "Cliente" """, "tm.id_tipo_maquina, tm.nombre", "2 DESC, tm.nombre", "");
    private static readonly string SqlMaquinariaPorActivo = SqlMaquinaria(
        """a.id_activo AS "Id", COALESCE(a.nombre, a.num_serie) AS "Nombre", cl.razon_social AS "Cliente" """,
        "a.id_activo, a.nombre, a.num_serie, cl.razon_social", "7 DESC, 2 DESC, a.id_activo", "LIMIT @limite");

    private readonly SotraContext _contexto;

    public AnaliticaDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<List<PuntoTendenciaFila>> ObtenerTendenciasAsync(
        string unidad, FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var (_, parametros) = Condiciones(filtro, "o");
        parametros.Add(new NpgsqlParameter("unidad", unidad));
        return await _contexto.Database.SqlQueryRaw<PuntoTendenciaFila>(SqlTendencias, parametros.ToArray<object>())
            .ToListAsync(cancelacion);
    }

    public async Task<int> ContarOrdenesAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var (_, parametros) = Condiciones(filtro, "o");
        return await _contexto.Database.SqlQueryRaw<int>(SqlContarOrdenes, parametros.ToArray<object>())
            .SingleAsync(cancelacion);
    }

    public async Task<DuracionFila> ObtenerDuracionDeOrdenesCerradasAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var (_, parametros) = Condiciones(filtro, "o");
        return await _contexto.Database.SqlQueryRaw<DuracionFila>(SqlDuracion, parametros.ToArray<object>())
            .SingleAsync(cancelacion);
    }

    /// <summary>Tiempo promedio que las órdenes pasan en cada estado, a partir de las fechas del historial.</summary>
    public async Task<List<EtapaFila>> ObtenerTiempoPorEtapaAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var (_, parametros) = Condiciones(filtro, "o");
        return await _contexto.Database.SqlQueryRaw<EtapaFila>(SqlEtapas, parametros.ToArray<object>())
            .ToListAsync(cancelacion);
    }

    /// <summary>
    /// Activos con correctivas que se repiten: una orden correctiva creada dentro de <paramref name="dias"/> días
    /// del cierre (o, si no cerró, de la creación) de la correctiva anterior del mismo activo.
    /// </summary>
    public async Task<List<ReincidenciaFila>> ObtenerReincidenciasAsync(
        int dias, int minimoDeReincidencias, int limite, FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var (_, parametros) = Condiciones(filtro, "o");
        parametros.Add(new NpgsqlParameter("dias", dias));
        parametros.Add(new NpgsqlParameter("minimo", minimoDeReincidencias));
        parametros.Add(new NpgsqlParameter("limite", limite));
        return await _contexto.Database.SqlQueryRaw<ReincidenciaFila>(SqlReincidencias, parametros.ToArray<object>())
            .ToListAsync(cancelacion);
    }

    /// <summary>Órdenes que siguen abiertas (estados de fase pendiente o en curso) con el tiempo que llevan en su estado actual.</summary>
    public async Task<List<OrdenAbiertaFila>> ListarOrdenesAbiertasAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var (_, parametros) = Condiciones(filtro, "o");
        return await _contexto.Database.SqlQueryRaw<OrdenAbiertaFila>(SqlOrdenesAbiertas, parametros.ToArray<object>())
            .ToListAsync(cancelacion);
    }

    public async Task<CumplimientoFila> ObtenerCumplimientoDeFechasAsync(FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var (_, parametros) = Condiciones(filtro, "o");
        return await _contexto.Database.SqlQueryRaw<CumplimientoFila>(SqlCumplimiento, parametros.ToArray<object>())
            .SingleAsync(cancelacion);
    }

    /// <summary>Repuestos asociados a más órdenes (la cantidad real consumida llega con los egresos de stock).</summary>
    public async Task<List<RepuestoUsadoFila>> ObtenerRepuestosMasUsadosAsync(
        int limite, FiltroAnalitica filtro, CancellationToken cancelacion)
    {
        var (_, parametros) = Condiciones(filtro, "o");
        parametros.Add(new NpgsqlParameter("limite", limite));
        return await _contexto.Database.SqlQueryRaw<RepuestoUsadoFila>(SqlRepuestosUsados, parametros.ToArray<object>())
            .ToListAsync(cancelacion);
    }

    public async Task<ResumenDeOrdenesFila> ObtenerResumenDeOrdenesAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<ResumenDeOrdenesFila>(SqlResumenDeOrdenes, filtro).SingleAsync(cancelacion);

    /// <summary>Horas entre la creación y el inicio, el inicio y la primera revisión, y la primera revisión y el cierre.</summary>
    public async Task<(DuracionFila Respuesta, DuracionFila Ejecucion, DuracionFila Revision)> ObtenerTiemposDeProcesoAsync(
        FiltroAnalitica filtro, CancellationToken cancelacion) =>
        (await Consultar<DuracionFila>(SqlRespuesta, filtro).SingleAsync(cancelacion),
         await Consultar<DuracionFila>(SqlEjecucion, filtro).SingleAsync(cancelacion),
         await Consultar<DuracionFila>(SqlRevision, filtro).SingleAsync(cancelacion));

    public async Task<CambiosDeProcesoFila> ObtenerPausasAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<CambiosDeProcesoFila>(SqlTotalPausas, filtro).SingleAsync(cancelacion);

    public async Task<CambiosDeProcesoFila> ObtenerDevolucionesAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<CambiosDeProcesoFila>(SqlTotalDevoluciones, filtro).SingleAsync(cancelacion);

    public async Task<List<MotivoFila>> ObtenerMotivosDePausaAsync(int limite, FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<MotivoFila>(SqlMotivosDePausa, filtro, new NpgsqlParameter("limite", limite)).ToListAsync(cancelacion);

    public async Task<List<MotivoFila>> ObtenerMotivosDeDevolucionAsync(int limite, FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<MotivoFila>(SqlMotivosDeDevolucion, filtro, new NpgsqlParameter("limite", limite)).ToListAsync(cancelacion);

    public async Task<ReasignacionesFila> ObtenerReasignacionesAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<ReasignacionesFila>(SqlReasignaciones, filtro).SingleAsync(cancelacion);

    public async Task<List<MaquinariaFila>> ObtenerMaquinariaPorTipoAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<MaquinariaFila>(SqlMaquinariaPorTipo, filtro).ToListAsync(cancelacion);

    public async Task<List<MaquinariaFila>> ObtenerMaquinariaPorActivoAsync(int limite, FiltroAnalitica filtro, CancellationToken cancelacion) =>
        await Consultar<MaquinariaFila>(SqlMaquinariaPorActivo, filtro, new NpgsqlParameter("limite", limite)).ToListAsync(cancelacion);

    private IQueryable<T> Consultar<T>(string sql, FiltroAnalitica filtro, params NpgsqlParameter[] extra)
    {
        var (_, parametros) = Condiciones(filtro, "o");
        parametros.AddRange(extra);
        return _contexto.Database.SqlQueryRaw<T>(sql, parametros.ToArray<object>());
    }

    private static (string Alias, List<NpgsqlParameter> Parametros) Condiciones(FiltroAnalitica filtro, string alias)
    {
        return (alias, new List<NpgsqlParameter>
        {
            Parametro("desde", NpgsqlDbType.TimestampTz,
                filtro.Desde?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            Parametro("hasta", NpgsqlDbType.TimestampTz,
                filtro.Hasta?.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
            Parametro("clienteId", NpgsqlDbType.Bigint, filtro.ClienteId),
            Parametro("activoId", NpgsqlDbType.Bigint, filtro.ActivoId),
        });
    }

    private static NpgsqlParameter Parametro(string nombre, NpgsqlDbType tipo, object? valor)
    {
        return new NpgsqlParameter(nombre, tipo) { Value = valor ?? DBNull.Value };
    }
}
