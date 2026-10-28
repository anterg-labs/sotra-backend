using Microsoft.EntityFrameworkCore;
using Npgsql;
using SOTRA_BACKEND.Contexto;

namespace SOTRA_BACKEND.Operaciones;

public sealed class DemorasEtapasFila
{
    public string Resultado { get; set; } = "";
}

/// <summary>Agrega por orden/etapa en PostgreSQL; una sentencia mantiene estadísticas, referencia y página coherentes.</summary>
public class DemorasEtapasDAO(SotraContext contexto)
{
    private const string Consulta = $"""
        WITH catalogo(etapa, nombre, posicion) AS (
            VALUES ('pendiente', 'Pendiente', 1), ('en_progreso', 'En progreso', 2),
                   ('revision', 'Revisión', 3), ('pausa', 'Pausa', 4)
        ), historial AS (
            SELECT h.id_orden, h.fecha_hora,
                   lead(h.fecha_hora) OVER (PARTITION BY h.id_orden ORDER BY h.fecha_hora, h.id_h_estado_ot) AS siguiente,
                   CASE WHEN e.fase = 'pendiente' THEN 'pendiente'
                        WHEN e.fase = 'por_validar' THEN 'revision'
                        WHEN e.fase = 'en_curso' AND e.codigo_alerta = 'pausada_demasiado' THEN 'pausa'
                        WHEN e.fase = 'en_curso' THEN 'en_progreso' END AS etapa
            FROM operativo.historial_estado_ot AS h
            JOIN operativo.estado_ot AS e ON e.id_estado_ot = h.id_estado_ot
            WHERE h.fecha_hora <= @ahora
        ), tramos AS (
            SELECT h.id_orden, h.etapa,
                   EXTRACT(EPOCH FROM (least(COALESCE(h.siguiente, @ahora), COALESCE(o.fecha_cierre, @ahora), @ahora)
                       - greatest(h.fecha_hora, o.fecha_creada)))::float8 / 3600.0 AS horas,
                   h.siguiente IS NULL AND o.fecha_cierre IS NULL AS en_curso
            FROM historial AS h
            JOIN operativo.orden_trabajo AS o ON o.id_orden = h.id_orden
            WHERE h.etapa IS NOT NULL
              AND least(COALESCE(h.siguiente, @ahora), COALESCE(o.fecha_cierre, @ahora), @ahora) >= greatest(h.fecha_hora, o.fecha_creada)
        ), por_orden AS (
            SELECT id_orden, etapa, sum(horas) AS horas, bool_or(en_curso) AS en_curso
            FROM tramos GROUP BY id_orden, etapa
        ), historico AS (
            SELECT etapa, avg(horas) AS media, count(*)::int AS muestra
            FROM por_orden WHERE NOT en_curso GROUP BY etapa
        ), referencias AS (
            SELECT c.*, h.media, COALESCE(h.muestra, 0) AS muestra,
                   COALESCE(u.umbral_horas, h.media) AS umbral,
                   CASE WHEN u.umbral_horas IS NOT NULL THEN 'configurado'
                        WHEN h.media IS NOT NULL THEN 'historico' ELSE 'sin_datos' END AS origen
            FROM catalogo AS c
            LEFT JOIN historico AS h USING (etapa)
            LEFT JOIN operativo.umbral_etapa_ot AS u USING (etapa)
        ), seleccion AS (
            SELECT p.*, o.id_activo, COALESCE(a.nombre, a.num_serie) AS activo, r.umbral,
                   p.horas > r.umbral AS demorada
            FROM por_orden AS p
            JOIN operativo.orden_trabajo AS o ON o.id_orden = p.id_orden
            JOIN operativo.activo AS a ON a.id_activo = o.id_activo
            JOIN referencias AS r USING (etapa)
            WHERE TRUE {AnaliticaDAO.FiltrosDeOrden}
        ), estadisticas AS (
            SELECT r.etapa AS "Etapa", r.nombre AS "Nombre", r.posicion,
                   count(s.id_orden)::int AS "CantidadOrdenes",
                   avg(s.horas) AS "PromedioHoras",
                   percentile_cont(0.5) WITHIN GROUP (ORDER BY s.horas) AS "MedianaHoras",
                   percentile_cont(0.9) WITHIN GROUP (ORDER BY s.horas) AS "Percentil90Horas",
                   r.media AS "PromedioHistoricoHoras", r.muestra AS "MuestraHistorica",
                   r.umbral AS "UmbralHoras", r.origen AS "OrigenUmbral",
                   (count(s.id_orden) FILTER (WHERE s.demorada))::int AS "Demoradas"
            FROM referencias AS r LEFT JOIN seleccion AS s ON s.etapa = r.etapa
            GROUP BY r.etapa, r.nombre, r.posicion, r.media, r.muestra, r.umbral, r.origen
        ), pagina AS (
            SELECT id_orden AS "OrdenId", id_activo AS "ActivoId", activo AS "NombreActivo", etapa AS "Etapa",
                   horas AS "Horas", umbral AS "UmbralHoras", horas - umbral AS "ExcesoHoras", en_curso AS "EnCurso"
            FROM seleccion WHERE demorada AND (@etapa IS NULL OR etapa = @etapa)
            ORDER BY horas - umbral DESC, id_orden, etapa
            LIMIT @tamano OFFSET @desplazamiento
        ), total AS (
            SELECT count(*)::int AS cantidad FROM seleccion WHERE demorada AND (@etapa IS NULL OR etapa = @etapa)
        )
        SELECT jsonb_build_object(
            'CalculadoEn', @ahora,
            'Etapas', (SELECT jsonb_agg(to_jsonb(e) - 'posicion' ORDER BY e.posicion) FROM estadisticas AS e),
            'Demoradas', jsonb_build_object(
                'Elementos', COALESCE((SELECT jsonb_agg(to_jsonb(p)) FROM pagina AS p), '[]'::jsonb),
                'Pagina', @pagina, 'TamanoPagina', @tamano, 'TotalElementos', total.cantidad,
                'TotalPaginas', (total.cantidad + @tamano - 1) / @tamano)
        )::text AS "Resultado" FROM total
        """;

    public async Task<string> ObtenerAsync(FiltroAnalitica filtro, string? etapa, int pagina, int tamano, DateTime ahora, CancellationToken cancelacion)
    {
        var parametros = AnaliticaDAO.ParametrosDelFiltro(filtro);
        parametros.AddRange([
            new NpgsqlParameter("ahora", ahora),
            new NpgsqlParameter("etapa", NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)etapa ?? DBNull.Value },
            new NpgsqlParameter("pagina", pagina), new NpgsqlParameter("tamano", tamano),
            new NpgsqlParameter("desplazamiento", ((long)pagina - 1) * tamano),
        ]);
        return (await contexto.Database.SqlQueryRaw<DemorasEtapasFila>(Consulta, parametros.ToArray<object>()).SingleAsync(cancelacion)).Resultado;
    }
}
