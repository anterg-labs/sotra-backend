using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;

namespace SOTRA_BACKEND.Operaciones;

public sealed class ConsumoDeRepuestoFila
{
    public long RepuestoId { get; set; }
    public string Codigo { get; set; } = "";
    public string Repuesto { get; set; } = "";
    public long Unidades { get; set; }
    public int Ordenes { get; set; }
}

/// <summary>Cuenta unidades descontadas al cerrar órdenes, por fecha del egreso asociado.</summary>
public sealed class ConsumoDeRepuestosDAO(SotraContext contexto)
{
    private const string Consulta = """
        SELECT r.id_repuesto AS "RepuestoId", r.codigo AS "Codigo", r.nombre AS "Repuesto",
               sum(m.cantidad)::bigint AS "Unidades",
               count(DISTINCT o.id_orden)::int AS "Ordenes"
        FROM operativo.ot_repuesto AS vinculo
        JOIN operativo.orden_trabajo AS o ON o.id_orden = vinculo.id_orden
        JOIN operativo.movimiento_stock AS m ON m.id_movimiento = vinculo.id_movimiento
        JOIN operativo.repuesto AS r ON r.id_repuesto = vinculo.id_repuesto
        WHERE vinculo.estado = 'descontado' AND m.tipo = 'egreso'
          AND m.id_repuesto = vinculo.id_repuesto AND m.cantidad = vinculo.cantidad
          AND (@desde IS NULL OR m.fecha >= @desde)
          AND (@hasta IS NULL OR m.fecha < @hasta)
          AND (@clienteId IS NULL OR o.id_cliente = @clienteId)
          AND (@activoId IS NULL OR o.id_activo = @activoId)
        GROUP BY r.id_repuesto, r.codigo, r.nombre
        ORDER BY "Unidades" DESC, r.nombre, r.id_repuesto
        """;

    public Task<List<ConsumoDeRepuestoFila>> ObtenerAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        contexto.Database.SqlQueryRaw<ConsumoDeRepuestoFila>(Consulta,
            AnaliticaDAO.ParametrosDelFiltro(filtro).ToArray<object>()).ToListAsync(cancelacion);
}
