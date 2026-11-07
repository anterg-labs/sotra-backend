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
        var lineas = (await respuesta.Content.ReadAsStringAsync())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(respuesta.Content.Headers.ContentType!.MediaType, Is.EqualTo("application/x-ndjson"));
        Assert.That(lineas.Length, Is.GreaterThanOrEqualTo(2));
        using var primera = JsonDocument.Parse(lineas[0]);
        Assert.That(primera.RootElement.GetProperty("etapa").GetString(), Is.EqualTo("buscando"));
        using var ultima = JsonDocument.Parse(lineas[^1]);
        Assert.That(ultima.RootElement.TryGetProperty("sugerencia", out _) || ultima.RootElement.TryGetProperty("error", out _), Is.True);
    }
}
