using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class LimiteDeSolicitudesPruebas
{
    [Test]
    public async Task LasEscriturasQueSuperanElLimiteResponden429ConReintentoYLasLecturasNoSeFrenan()
    {
        EntornoDePruebas.VerificarBaseDePruebas();
        // Program.cs lee la configuración al arrancar, antes de que llegue ConfigureAppConfiguration: se usa la variable de entorno.
        const string variable = "LimiteSolicitudes__PorMinutoEscrituras";
        var valorOriginal = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "3");
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CreateClient();
        Environment.SetEnvironmentVariable(variable, valorOriginal);

        var respuestas = new List<HttpResponseMessage>();
        for (var i = 0; i < 6; i++)
        {
            respuestas.Add(await cliente.PostAsJsonAsync("/api/v1/tipos-maquina", new { nombre = "" }));
        }

        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests), Is.GreaterThanOrEqualTo(1));
        var rechazada = respuestas.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.That(rechazada.Headers.Contains("Retry-After"), Is.True);

        var lectura = await cliente.GetAsync("/api/v1/version");
        Assert.That(lectura.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task LasSugerenciasDeIaTienenSuPropioLimiteMasBajo()
    {
        EntornoDePruebas.VerificarBaseDePruebas();
        const string variable = "LimiteSolicitudes__PorMinutoSugerencias";
        var valorOriginal = Environment.GetEnvironmentVariable(variable);
        Environment.SetEnvironmentVariable(variable, "2");
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CreateClient();
        Environment.SetEnvironmentVariable(variable, valorOriginal);

        var respuestas = new List<HttpResponseMessage>();
        for (var i = 0; i < 5; i++)
        {
            respuestas.Add(await cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo/sugerencia",
                new { descripcion = "Cambiar el filtro", numero = 1 }));
        }

        var rechazada = respuestas.First(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        Assert.That(rechazada.Headers.Contains("Retry-After"), Is.True);
        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests), Is.GreaterThanOrEqualTo(2));
    }
}
