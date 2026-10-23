using System.Diagnostics.Metrics;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;

namespace Api.Infraestructura;

/// <summary>
/// Mide cada minuto el estado del negocio (órdenes por estado, tiempo de cierre y stock bajo) y lo expone como
/// medidores observables del medidor <c>Sotra.Api</c>. Solo se registra cuando la observabilidad está habilitada,
/// así que sin exportador no se hace ninguna consulta extra.
/// </summary>
public sealed class RecolectorDeMetricasDeNegocio : BackgroundService
{
    private const int SegundosEntreMediciones = 60;
    private const int SegundosMaximosPorMedicion = 10;
    private const int DiasParaElTiempoDeCierre = 7;

    private const string SqlOrdenesPorEstado = """
        SELECT e.nombre AS "Estado", count(*)::int AS "Cantidad"
        FROM operativo.orden_trabajo AS o
        JOIN LATERAL (
            SELECT h.id_estado_ot
            FROM operativo.historial_estado_ot AS h
            WHERE h.id_orden = o.id_orden
            ORDER BY h.fecha_hora DESC, h.id_h_estado_ot DESC
            LIMIT 1
        ) AS actual ON TRUE
        JOIN operativo.estado_ot AS e ON e.id_estado_ot = actual.id_estado_ot
        GROUP BY e.nombre
        """;

    private const string SqlHorasHastaElCierre = """
        SELECT avg(EXTRACT(EPOCH FROM (o.fecha_cierre - o.fecha_creada)) / 3600.0)::float8 AS "Value"
        FROM operativo.orden_trabajo AS o
        WHERE o.fecha_cierre IS NOT NULL AND o.fecha_cierre >= now() - interval '7 days'
        """;

    public sealed class FilaDeEstado
    {
        public string Estado { get; set; } = "";
        public int Cantidad { get; set; }
    }

    private sealed record Medicion(
        IReadOnlyList<KeyValuePair<string, int>> OrdenesPorEstado,
        double? HorasHastaElCierre,
        int RepuestosConStockBajo,
        int RepuestosSinStock);

    private readonly IServiceScopeFactory _alcances;
    private readonly ILogger<RecolectorDeMetricasDeNegocio> _registro;
    private readonly Meter _medidor = new(MetricasDeNegocio.NombreDelMedidor);
    private volatile Medicion _ultima = new([], null, 0, 0);

    public RecolectorDeMetricasDeNegocio(IServiceScopeFactory alcances, ILogger<RecolectorDeMetricasDeNegocio> registro)
    {
        _alcances = alcances;
        _registro = registro;

        _medidor.CreateObservableGauge<int>("sotra.ordenes.por_estado",
            () => _ultima.OrdenesPorEstado.Select(par =>
                new Measurement<int>(par.Value, new KeyValuePair<string, object?>("estado", par.Key))),
            description: "Órdenes de trabajo según su estado actual");
        _medidor.CreateObservableGauge<double>("sotra.ordenes.horas_hasta_el_cierre",
            () => _ultima.HorasHastaElCierre is { } horas ? new[] { new Measurement<double>(horas) } : [],
            unit: "h", description: $"Promedio de horas entre la creación y el cierre de las órdenes cerradas en los últimos {DiasParaElTiempoDeCierre} días");
        _medidor.CreateObservableGauge<int>("sotra.repuestos.con_stock_bajo",
            () => [new Measurement<int>(_ultima.RepuestosConStockBajo)],
            description: "Repuestos activos con stock en o por debajo del mínimo, sin contar los agotados");
        _medidor.CreateObservableGauge<int>("sotra.repuestos.sin_stock",
            () => [new Measurement<int>(_ultima.RepuestosSinStock)],
            description: "Repuestos activos agotados");
    }

    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        while (!cancelacion.IsCancellationRequested)
        {
            await MedirAsync(cancelacion);
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(SegundosEntreMediciones), cancelacion);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    internal async Task MedirAsync(CancellationToken cancelacion)
    {
        try
        {
            using var limite = CancellationTokenSource.CreateLinkedTokenSource(cancelacion);
            limite.CancelAfter(TimeSpan.FromSeconds(SegundosMaximosPorMedicion));

            await using var alcance = _alcances.CreateAsyncScope();
            var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();

            var porEstado = await contexto.Database.SqlQueryRaw<FilaDeEstado>(SqlOrdenesPorEstado).ToListAsync(limite.Token);
            var horas = await contexto.Database.SqlQueryRaw<double?>(SqlHorasHastaElCierre).SingleAsync(limite.Token);
            var activos = contexto.Repuestos.AsNoTracking().Where(r => r.Estado);
            var sinStock = await activos.CountAsync(r => r.Cantidad <= 0, limite.Token);
            var bajo = await activos.CountAsync(r => r.Cantidad > 0 && r.CantMin > 0 && r.Cantidad <= r.CantMin, limite.Token);

            _ultima = new Medicion(
                porEstado.Select(fila => new KeyValuePair<string, int>(fila.Estado, fila.Cantidad)).ToList(),
                horas is null ? null : Math.Round(horas.Value, 2),
                bajo,
                sinStock);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancelacion.IsCancellationRequested)
        {
            _registro.LogWarning(ex, "No se pudieron medir las métricas de negocio; se conserva la última medición.");
        }
    }

    public override void Dispose()
    {
        _medidor.Dispose();
        base.Dispose();
    }
}
