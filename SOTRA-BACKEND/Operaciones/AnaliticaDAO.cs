using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SOTRA_BACKEND.Contexto;

namespace SOTRA_BACKEND.Operaciones;

public sealed record FiltroAnalitica(DateOnly? Desde, DateOnly? Hasta, long? ClienteId, long? ActivoId, long? TecnicoId);

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

/// <summary>Consultas agregadas del dashboard: se resuelven en la base con GROUP BY y funciones de ventana.</summary>
public class AnaliticaDAO
{
    /// <summary>Filtros opcionales de las órdenes (alias <c>o</c>): cada uno se ignora si su parámetro es nulo.</summary>
    private const string FiltrosDeOrden = """
        AND (@desde IS NULL OR o.fecha_creada >= @desde)
        AND (@hasta IS NULL OR o.fecha_creada < @hasta)
        AND (@clienteId IS NULL OR o.id_cliente = @clienteId)
        AND (@activoId IS NULL OR o.id_activo = @activoId)
        AND (@tecnicoId IS NULL OR o.id_usuario_tecnico = @tecnicoId)
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
               e.codigo_alerta AS "CodigoAlerta",
               o.prioridad AS "Prioridad",
               GREATEST(0, EXTRACT(EPOCH FROM (now() - actual.fecha_hora)) / 3600.0)::float8 AS "HorasEnEstado",
               GREATEST(0, EXTRACT(EPOCH FROM (now() - o.fecha_creada)) / 3600.0)::float8 AS "HorasDesdeCreacion"
        FROM operativo.orden_trabajo AS o
        JOIN LATERAL (
            SELECT h.id_estado_ot, h.fecha_hora
            FROM operativo.historial_estado_ot AS h
            WHERE h.id_orden = o.id_orden
            ORDER BY h.fecha_hora DESC, h.id_h_estado_ot DESC
            LIMIT 1
        ) AS actual ON TRUE
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

    /// <summary>Órdenes que siguen abiertas (creada, asignada, en ejecución o pausada) con el tiempo que llevan en su estado actual.</summary>
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
            Parametro("tecnicoId", NpgsqlDbType.Bigint, filtro.TecnicoId),
        });
    }

    private static NpgsqlParameter Parametro(string nombre, NpgsqlDbType tipo, object? valor)
    {
        return new NpgsqlParameter(nombre, tipo) { Value = valor ?? DBNull.Value };
    }
}
