using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;

namespace SOTRA_BACKEND.Operaciones;

public sealed class DesgasteEdadUsoFila
{
    public long ActivoId { get; set; }
    public string NombreActivo { get; set; } = "";
    public string Modelo { get; set; } = "";
    public int? EdadAnios { get; set; }
    public decimal? LecturaUso { get; set; }
    public string UnidadUso { get; set; } = "";
    public DateTime? FechaLectura { get; set; }
    public int Correctivas { get; set; }
    public int OrdenesObservadas { get; set; }
}

/// <summary>Una observación por activo con órdenes en el período; no atribuye desempeño a personas.</summary>
public sealed class DesgasteEdadUsoDAO(SotraContext contexto)
{
    private const string Consulta = """
        WITH observadas AS (
            SELECT o.id_activo, count(*)::int AS correctivas_y_preventivas,
                   count(*) FILTER (WHERE NOT o.tipo)::int AS correctivas,
                   max(o.fecha_creada) AS ultima_orden
            FROM operativo.orden_trabajo o
            WHERE TRUE
        """ + "\n" + AnaliticaDAO.FiltrosDeOrden + """

            GROUP BY o.id_activo
        )
        SELECT a.id_activo AS "ActivoId",
               coalesce(nullif(a.nombre, ''), a.num_serie) AS "NombreActivo",
               m.nombre AS "Modelo",
               CASE WHEN a.anio_fabricacion IS NULL
                         OR a.anio_fabricacion > extract(year FROM obs.ultima_orden AT TIME ZONE 'UTC')
                    THEN NULL
                    ELSE (extract(year FROM obs.ultima_orden AT TIME ZONE 'UTC') - a.anio_fabricacion)::int
               END AS "EdadAnios",
               lectura.lectura_uso AS "LecturaUso",
               a.unidad_uso AS "UnidadUso",
               lectura.fecha_creada AS "FechaLectura",
               obs.correctivas AS "Correctivas",
               obs.correctivas_y_preventivas AS "OrdenesObservadas"
        FROM observadas obs
        JOIN operativo.activo a ON a.id_activo = obs.id_activo
        JOIN operativo.modelo_maquina m ON m.id_modelo_maquina = a.id_modelo_maquina
        LEFT JOIN LATERAL (
            SELECT o.lectura_uso, o.fecha_creada
            FROM operativo.orden_trabajo o
            WHERE o.id_activo = obs.id_activo AND o.lectura_uso IS NOT NULL
              AND o.fecha_creada <= obs.ultima_orden
            ORDER BY o.fecha_creada DESC, o.id_orden DESC
            LIMIT 1
        ) lectura ON TRUE
        ORDER BY obs.correctivas DESC, a.id_activo
        """;

    public Task<List<DesgasteEdadUsoFila>> ObtenerAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        contexto.Database.SqlQueryRaw<DesgasteEdadUsoFila>(Consulta,
            AnaliticaDAO.ParametrosDelFiltro(filtro).ToArray<object>()).ToListAsync(cancelacion);
}
