using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SOTRA_BACKEND.Contexto;

namespace SOTRA_BACKEND.Operaciones;

public sealed class ClienteDeReportesFila
{
    public long ClienteId { get; set; }
    public string Cliente { get; set; } = "";
    public int Ordenes { get; set; }
    public int Correctivas { get; set; }
    public int CorrectivasCerradas { get; set; }
    public double HorasParada { get; set; }
    public int Reincidencias { get; set; }
}

/// <summary>Agrega por empresa en PostgreSQL y reproduce la ventana de reincidencia de AnaliticaDAO.</summary>
public sealed class ResumenPorClienteDAO(SotraContext contexto)
{
    private const string Consulta = """
        WITH ordenes AS (
            SELECT o.id_orden, o.id_cliente, o.id_activo, o.tipo, o.fecha_creada, o.fecha_cierre
            FROM operativo.orden_trabajo AS o
            WHERE TRUE
        """ + "\n" + AnaliticaDAO.FiltrosDeOrden + """

        ), correctivas AS (
            SELECT o.id_cliente, o.id_activo, o.fecha_creada,
                   lag(coalesce(o.fecha_cierre, o.fecha_creada))
                       OVER (PARTITION BY o.id_activo ORDER BY o.fecha_creada, o.id_orden) AS anterior
            FROM ordenes AS o WHERE NOT o.tipo
        ), reincidencias AS (
            SELECT id_cliente, count(*) FILTER (WHERE anterior IS NOT NULL
                AND fecha_creada - anterior <= make_interval(days => @dias))::int AS cantidad
            FROM correctivas GROUP BY id_cliente
        ), resumen AS (
            SELECT o.id_cliente, count(*)::int AS ordenes,
                   count(*) FILTER (WHERE NOT o.tipo)::int AS correctivas,
                   count(*) FILTER (WHERE NOT o.tipo AND o.fecha_cierre IS NOT NULL)::int AS cerradas,
                   coalesce(sum(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0)
                       FILTER (WHERE NOT o.tipo AND o.fecha_cierre IS NOT NULL), 0)::float8 AS horas
            FROM ordenes AS o GROUP BY o.id_cliente
        )
        SELECT cl.id_cliente AS "ClienteId", cl.razon_social AS "Cliente",
               r.ordenes AS "Ordenes", r.correctivas AS "Correctivas",
               r.cerradas AS "CorrectivasCerradas", r.horas AS "HorasParada",
               coalesce(rep.cantidad, 0)::int AS "Reincidencias"
        FROM resumen AS r
        JOIN operativo.cliente AS cl ON cl.id_cliente = r.id_cliente
        LEFT JOIN reincidencias AS rep ON rep.id_cliente = r.id_cliente
        ORDER BY r.correctivas DESC, r.horas DESC, cl.razon_social, cl.id_cliente
        """;

    public Task<List<ClienteDeReportesFila>> ObtenerAsync(FiltroAnalitica filtro, int dias, CancellationToken cancelacion)
    {
        var parametros = AnaliticaDAO.ParametrosDelFiltro(filtro);
        parametros.Add(new NpgsqlParameter("dias", NpgsqlDbType.Integer) { Value = dias });
        return contexto.Database.SqlQueryRaw<ClienteDeReportesFila>(Consulta, parametros.ToArray<object>())
            .ToListAsync(cancelacion);
    }
}
