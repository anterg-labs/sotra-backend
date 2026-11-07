using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class EtapasDeSugerenciaPruebas
{
    private const string Ruta = "/api/v1/ordenes-trabajo/sugerencia/etapas";

    [Test]
    public async Task SinDescripcionRespondeElErrorDeSiempreSinEmpezarAInformarEtapas()
    {
        EntornoDePruebas.VerificarBaseDePruebas();
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(Ruta, new { descripcion = " ", numero = 1 });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task InformaLaPrimeraEtapaYTerminaConLaSugerenciaOConElError()
    {
        EntornoDePruebas.VerificarBaseDePruebas();
        await using var fabrica = new FabricaApi();
        using var cliente = fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(Ruta, new { descripcion = "Cambiar el filtro", numero = 1 });
        var eventos = (await respuesta.Content.ReadAsStringAsync())
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(respuesta.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/event-stream"),
            "Cloudflare junta las etapas de otros formatos");
        Assert.That(respuesta.Headers.CacheControl!.NoTransform, Is.True, "sin no-transform, los intermediarios comprimen y juntan las etapas");
        Assert.That(respuesta.Headers.GetValues("X-Accel-Buffering").Single(), Is.EqualTo("no"));
        Assert.That(eventos.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(eventos, Has.All.StartsWith("data: "), "cada evento es una línea data: con su JSON");
        using var primera = JsonDocument.Parse(eventos[0]["data: ".Length..]);
        Assert.That(primera.RootElement.GetProperty("etapa").GetString(), Is.EqualTo("buscando"));
        using var ultima = JsonDocument.Parse(eventos[^1]["data: ".Length..]);
        Assert.That(ultima.RootElement.TryGetProperty("sugerencia", out _) || ultima.RootElement.TryGetProperty("error", out _), Is.True);
    }
}
