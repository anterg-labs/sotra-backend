using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SOTRA_BACKEND.Contexto;

namespace Api.Pruebas.Caracterizacion;

public class TerminosPruebas : PruebaBase
{
    private async Task<JsonElement> GuardarAsync(string texto = "Texto completo de los términos y condiciones.")
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/terminos", new { texto });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        return await respuesta.LeerJsonAsync();
    }

    private async Task<long> CrearUsuarioAsync() =>
        (await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [3]))).GetProperty("id").GetInt64();

    private Task<HttpResponseMessage> AceptarAsync(long usuarioId, JsonElement documento, bool acepta = true) =>
        Cliente.PostAsJsonAsync("/api/v1/terminos/aceptaciones", new
        {
            usuarioId, terminosId = documento.GetProperty("id").GetInt64(),
            version = documento.GetProperty("version").GetInt32(), acepta
        });

    private async Task<JsonElement> EstadoAsync(long usuarioId) =>
        await (await Cliente.GetAsync($"/api/v1/terminos/estado?usuarioId={usuarioId}")).LeerJsonAsync();

    private static async Task ComprobarDocumentoUnicoAsync()
    {
        await using var alcance = EntornoDePruebas.Fabrica.Services.CreateAsyncScope();
        Assert.That(await alcance.ServiceProvider.GetRequiredService<SotraContext>().TerminosCondiciones.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public async Task ElDocumentoExponeSoloTextoSinTituloDatosNiAutor()
    {
        var documento = await GuardarAsync();
        Assert.That(documento.GetProperty("texto").GetString(), Is.Not.Empty);
        Assert.That(documento.TryGetProperty("titulo", out _), Is.False);
        Assert.That(documento.TryGetProperty("datosAlmacenados", out _), Is.False);
        Assert.That(documento.TryGetProperty("autorId", out _), Is.False);
        Assert.That((await Cliente.GetAsync("/api/v1/terminos")).StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
    }

    [Test]
    public async Task GuardarReemplazaElDocumentoSinGenerarHistorial()
    {
        var anterior = await GuardarAsync("Primer texto completo para la prueba.");
        var actual = await GuardarAsync("Segundo texto completo para la prueba.");
        Assert.That(actual.GetProperty("id").GetInt64(), Is.EqualTo(anterior.GetProperty("id").GetInt64()));
        Assert.That(actual.GetProperty("version").GetInt32(), Is.EqualTo(anterior.GetProperty("version").GetInt32() + 1));
        await ComprobarDocumentoUnicoAsync();
    }

    [Test]
    public async Task ModificacionesConcurrentesConservanUnSoloDocumento()
    {
        var respuestas = await Task.WhenAll(Enumerable.Range(1, 5).Select(n =>
            Cliente.PostAsJsonAsync("/api/v1/terminos", new { texto = $"Texto de la modificación concurrente número {n}." })));
        Assert.That(respuestas.All(r => r.StatusCode == HttpStatusCode.OK), Is.True);
        var revisiones = new List<int>();
        foreach (var respuesta in respuestas)
            revisiones.Add((await respuesta.LeerJsonAsync()).GetProperty("version").GetInt32());
        Assert.That(revisiones.Distinct().Count(), Is.EqualTo(5));
        await ComprobarDocumentoUnicoAsync();
    }

    [TestCase("")]
    [TestCase(" ")]
    [TestCase("Corto")]
    public async Task TextoInvalidoResponde400(string texto) =>
        Assert.That((await Cliente.PostAsJsonAsync("/api/v1/terminos", new { texto })).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

    [Test]
    public async Task SeRespetanLosLimitesDelTexto()
    {
        await GuardarAsync(new string('a', 20));
        await GuardarAsync(new string('a', 20000));
        Assert.That((await Cliente.PostAsJsonAsync("/api/v1/terminos", new { texto = new string('a', 20001) })).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AceptarEsExplicitoEIdempotente()
    {
        var documento = await GuardarAsync();
        var usuarioId = await CrearUsuarioAsync();
        Assert.That((await EstadoAsync(usuarioId)).GetProperty("pendiente").GetBoolean(), Is.True);
        Assert.That((await AceptarAsync(usuarioId, documento, false)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await AceptarAsync(usuarioId, documento)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That((await AceptarAsync(usuarioId, documento)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await EstadoAsync(usuarioId)).GetProperty("pendiente").GetBoolean(), Is.False);
    }

    [Test]
    public async Task ModificarReiniciaLaAceptacionYRechazaElTextoAnterior()
    {
        var documento = await GuardarAsync("Contenido anterior para esta prueba.");
        var usuarioId = await CrearUsuarioAsync();
        (await AceptarAsync(usuarioId, documento)).EnsureSuccessStatusCode();
        await GuardarAsync("Contenido modificado para esta prueba.");
        Assert.That((await EstadoAsync(usuarioId)).GetProperty("pendiente").GetBoolean(), Is.True);
        Assert.That((await AceptarAsync(usuarioId, documento)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task GuardarSinCambiosConservaLaAceptacion()
    {
        const string texto = "Texto que se guarda sin modificaciones.";
        var documento = await GuardarAsync(texto);
        var usuarioId = await CrearUsuarioAsync();
        (await AceptarAsync(usuarioId, documento)).EnsureSuccessStatusCode();
        var repetido = await GuardarAsync(texto);
        Assert.That(repetido.GetProperty("version").GetInt32(), Is.EqualTo(documento.GetProperty("version").GetInt32()));
        Assert.That((await EstadoAsync(usuarioId)).GetProperty("pendiente").GetBoolean(), Is.False);
    }

    [Test]
    public async Task NoSePuedeAceptarSinIndicarElContenidoLeidoONiConUsuarioInexistente()
    {
        var documento = await GuardarAsync();
        Assert.That((await AceptarAsync(999999, documento)).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/terminos/aceptaciones", new
        {
            usuarioId = await CrearUsuarioAsync(), terminosId = documento.GetProperty("id").GetInt64(), acepta = true
        });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task AceptarDuranteUnaModificacionNoAceptaUnContenidoDiferente()
    {
        var documento = await GuardarAsync("Antes de la modificación concurrente.");
        var usuarioId = await CrearUsuarioAsync();
        var aceptacion = AceptarAsync(usuarioId, documento);
        await GuardarAsync("Después de la modificación concurrente.");
        Assert.That((await aceptacion).StatusCode, Is.AnyOf(HttpStatusCode.Created, HttpStatusCode.Conflict));
        Assert.That((await EstadoAsync(usuarioId)).GetProperty("pendiente").GetBoolean(), Is.True);
    }

    [Test]
    public async Task ElTextoHostilEsDatoYNuncaSeTomaElAutorDelCliente()
    {
        var texto = "<script>alert(1)</script> ' OR '1'='1";
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/terminos", new { texto, autorId = 999999 });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("texto").GetString(), Is.EqualTo(texto));
    }
}
