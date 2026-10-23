using System.Diagnostics.Metrics;
using Api.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SOTRA_BACKEND.Contexto;

namespace Api.Pruebas.Caracterizacion;

public class MetricasDeNegocioPruebas
{
    [Test]
    public async Task LasMedicionesDeNegocioCoincidenConLaBase()
    {
        using var recolector = new RecolectorDeMetricasDeNegocio(
            EntornoDePruebas.Fabrica.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<RecolectorDeMetricasDeNegocio>.Instance);
        await recolector.MedirAsync(CancellationToken.None);

        var enteros = new List<(string Nombre, int Valor, string? Estado)>();
        var decimales = new List<(string Nombre, double Valor)>();
        using var oyente = new MeterListener();
        oyente.InstrumentPublished = (instrumento, escucha) =>
        {
            if (instrumento.Meter.Name == MetricasDeNegocio.NombreDelMedidor)
            {
                escucha.EnableMeasurementEvents(instrumento);
            }
        };
        oyente.SetMeasurementEventCallback<int>((instrumento, valor, etiquetas, _) =>
            enteros.Add((instrumento.Name, valor, etiquetas.ToArray().FirstOrDefault(e => e.Key == "estado").Value as string)));
        oyente.SetMeasurementEventCallback<double>((instrumento, valor, _, _) => decimales.Add((instrumento.Name, valor)));
        oyente.Start();
        oyente.RecordObservableInstruments();

        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        var totalDeOrdenes = await contexto.OrdenTrabajos.AsNoTracking().CountAsync();
        var sinStock = await contexto.Repuestos.AsNoTracking().CountAsync(r => r.Estado && r.Cantidad <= 0);
        var bajo = await contexto.Repuestos.AsNoTracking()
            .CountAsync(r => r.Estado && r.Cantidad > 0 && r.CantMin > 0 && r.Cantidad <= r.CantMin);

        Assert.That(enteros.Where(m => m.Nombre == "sotra.ordenes.por_estado").Sum(m => m.Valor), Is.EqualTo(totalDeOrdenes));
        Assert.That(enteros.Where(m => m.Nombre == "sotra.ordenes.por_estado").All(m => !string.IsNullOrEmpty(m.Estado)), Is.True);
        Assert.That(enteros.Single(m => m.Nombre == "sotra.repuestos.sin_stock").Valor, Is.EqualTo(sinStock));
        Assert.That(enteros.Single(m => m.Nombre == "sotra.repuestos.con_stock_bajo").Valor, Is.EqualTo(bajo));
        Assert.That(decimales.Count(m => m.Nombre == "sotra.ordenes.horas_hasta_el_cierre"), Is.LessThanOrEqualTo(1));
    }
}
