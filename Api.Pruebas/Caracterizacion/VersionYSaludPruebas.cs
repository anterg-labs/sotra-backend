using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class VersionYSaludPruebas : PruebaBase
{
    [Test]
    public async Task ObtenerVersionResponde200ConLaVersionDelBackend()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/version");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(cuerpo.GetProperty("version").GetString(), Does.Match(@"^\d+\.\d+\.\d+$"));
    }

    [Test]
    public async Task ObtenerSaludResponde200()
    {
        var respuesta = await Cliente.GetAsync("/health");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await respuesta.Content.ReadAsStringAsync(), Is.EqualTo("Healthy"));
    }
}
