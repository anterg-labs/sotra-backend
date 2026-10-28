using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;

namespace SOTRA_BACKEND.Operaciones;

public sealed class RepuestoPorModeloFila
{
    public long ModeloId { get; set; }
    public string Modelo { get; set; } = "";
    public long TipoMaquinaId { get; set; }
    public string TipoMaquina { get; set; } = "";
    public long RepuestoId { get; set; }
    public string Codigo { get; set; } = "";
    public string Repuesto { get; set; } = "";
    public int OrdenesAsociadas { get; set; }
    public int OrdenesSuministradas { get; set; }
}

/// <summary>Agrega asociaciones de repuestos sin cargar órdenes ni personas en memoria.</summary>
public sealed class RepuestosPorModeloDAO(SotraContext contexto)
{
    private const string Consulta = """
        SELECT m.id_modelo_maquina AS "ModeloId", m.nombre AS "Modelo",
               t.id_tipo_maquina AS "TipoMaquinaId", t.nombre AS "TipoMaquina",
               r.id_repuesto AS "RepuestoId", r.codigo AS "Codigo", r.nombre AS "Repuesto",
               count(DISTINCT o.id_orden)::int AS "OrdenesAsociadas",
               count(DISTINCT o.id_orden) FILTER (WHERE vinculo.estado IN ('entregado', 'descontado'))::int AS "OrdenesSuministradas"
        FROM operativo.orden_trabajo AS o
        JOIN operativo.activo AS a ON a.id_activo = o.id_activo
        JOIN operativo.modelo_maquina AS m ON m.id_modelo_maquina = a.id_modelo_maquina
        JOIN operativo.tipo_maquina AS t ON t.id_tipo_maquina = m.id_tipo_maquina
        JOIN operativo.ot_repuesto AS vinculo ON vinculo.id_orden = o.id_orden
        JOIN operativo.repuesto AS r ON r.id_repuesto = vinculo.id_repuesto
        WHERE o.tipo = FALSE AND vinculo.estado <> 'liberado'
        """ + "\n" + AnaliticaDAO.FiltrosDeOrden + """

        GROUP BY m.id_modelo_maquina, m.nombre, t.id_tipo_maquina, t.nombre,
                 r.id_repuesto, r.codigo, r.nombre
        ORDER BY t.nombre, m.nombre, m.id_modelo_maquina, "OrdenesAsociadas" DESC, r.nombre, r.id_repuesto
        """;

    public Task<List<RepuestoPorModeloFila>> ObtenerAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        contexto.Database.SqlQueryRaw<RepuestoPorModeloFila>(Consulta,
            AnaliticaDAO.ParametrosDelFiltro(filtro).ToArray<object>()).ToListAsync(cancelacion);
}
