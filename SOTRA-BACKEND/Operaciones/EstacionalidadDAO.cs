using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;

namespace SOTRA_BACKEND.Operaciones;

public sealed class EstacionalidadFila
{
    public long TipoMaquinaId { get; set; }
    public string TipoMaquina { get; set; } = "";
    public int Mes { get; set; }
    public int Correctivas { get; set; }
}

/// <summary>Agrega en PostgreSQL; nunca carga las órdenes individuales ni datos de personas.</summary>
public sealed class EstacionalidadDAO(SotraContext contexto)
{
    private const string Consulta = """
        SELECT t.id_tipo_maquina AS "TipoMaquinaId", t.nombre AS "TipoMaquina",
               extract(month FROM o.fecha_creada AT TIME ZONE 'UTC')::int AS "Mes",
               count(*)::int AS "Correctivas"
        FROM operativo.orden_trabajo o
        JOIN operativo.activo a ON a.id_activo = o.id_activo
        JOIN operativo.modelo_maquina m ON m.id_modelo_maquina = a.id_modelo_maquina
        JOIN operativo.tipo_maquina t ON t.id_tipo_maquina = m.id_tipo_maquina
        WHERE o.tipo = FALSE
        """ + "\n" + AnaliticaDAO.FiltrosDeOrden + """

        GROUP BY t.id_tipo_maquina, t.nombre, extract(month FROM o.fecha_creada AT TIME ZONE 'UTC')
        ORDER BY t.nombre, t.id_tipo_maquina, "Mes"
        """;

    public Task<List<EstacionalidadFila>> ObtenerAsync(FiltroAnalitica filtro, CancellationToken cancelacion) =>
        contexto.Database.SqlQueryRaw<EstacionalidadFila>(Consulta,
            AnaliticaDAO.ParametrosDelFiltro(filtro).ToArray<object>()).ToListAsync(cancelacion);
}
