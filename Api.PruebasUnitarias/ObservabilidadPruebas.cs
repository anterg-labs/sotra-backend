using System.Diagnostics.Metrics;
using Api.Infraestructura;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Api.PruebasUnitarias;

public class ObservabilidadPruebas
{
    private static ServiceCollection Registrar(Dictionary<string, string?> valores)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var servicios = new ServiceCollection();
        servicios.AgregarObservabilidad(configuracion);
        return servicios;
    }

    [Test]
    public void SinDestinoConfiguradoNoSeRegistraNada()
    {
        Assert.That(Registrar([]), Is.Empty);
    }

    [Test]
    public void ConUnDestinoOtlpSeRegistraOpenTelemetry()
    {
        var servicios = Registrar(new() { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317" });

        Assert.That(servicios, Is.Not.Empty);
    }

    [Test]
    public void SePuedeApagarAunqueHayaDestino()
    {
        var servicios = Registrar(new()
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317",
            ["Observabilidad:Habilitada"] = "false",
        });

        Assert.That(servicios, Is.Empty);
    }

    [Test]
    public void SePuedeEncenderSinDestinoExplicito()
    {
        Assert.That(Registrar(new() { ["Observabilidad:Habilitada"] = "true" }), Is.Not.Empty);
    }

    [TestCase(200, "2xx")]
    [TestCase(204, "2xx")]
    [TestCase(404, "4xx")]
    [TestCase(409, "4xx")]
    [TestCase(500, "5xx")]
    public void LaClaseDeResultadoAgrupaElCodigoHttp(int codigo, string esperado)
    {
        Assert.That(MetricasDeNegocio.ClaseDeResultado(codigo), Is.EqualTo(esperado));
    }

    [Test]
    public void LasMetricasDeNegocioSeMidenConSusEtiquetas()
    {
        var medidas = new List<(string Nombre, long Valor, string? Resultado)>();
        using var escucha = new MeterListener();
        escucha.InstrumentPublished = (instrumento, oyente) =>
        {
            if (instrumento.Meter.Name == MetricasDeNegocio.NombreDelMedidor)
            {
                oyente.EnableMeasurementEvents(instrumento);
            }
        };
        escucha.SetMeasurementEventCallback<long>((instrumento, valor, etiquetas, _) =>
        {
            string? resultado = null;
            foreach (var etiqueta in etiquetas)
            {
                if (etiqueta.Key == "resultado")
                {
                    resultado = etiqueta.Value?.ToString();
                }
            }

            medidas.Add((instrumento.Name, valor, resultado));
        });
        escucha.Start();

        MetricasDeNegocio.Acciones.Add(1, new KeyValuePair<string, object?>("resultado", "2xx"));
        MetricasDeNegocio.EntradasRechazadas.Add(1);

        Assert.That(medidas, Does.Contain(("sotra.acciones", 1L, "2xx")));
        Assert.That(medidas.Select(m => m.Nombre), Does.Contain("sotra.entradas_rechazadas"));
    }
}
