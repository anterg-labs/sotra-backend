using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Infraestructura;
using Api.Servicios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using SOTRA_BACKEND.Contexto;

namespace Api.Pruebas.Caracterizacion;

/// <summary>
/// Con R2 configurado, los archivos de las evidencias van al almacén (carpeta evidencias/) y no a la base.
/// Usa un almacén en memoria en lugar del bucket real.
/// </summary>
public class EvidenciasEnR2Pruebas
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] Pdf = "%PDF-1.4\n%%EOF"u8.ToArray();

    private readonly AlmacenEnMemoria _almacen = new();
    private WebApplicationFactory<PuntoDeEntrada> _fabrica = null!;
    private HttpClient _cliente = null!;

    [OneTimeSetUp]
    public void Preparar()
    {
        _fabrica = EntornoDePruebas.Fabrica.WithWebHostBuilder(constructor =>
            constructor.ConfigureTestServices(servicios =>
                servicios.AddSingleton<IAlmacenDeArchivos>(_almacen)));
        _cliente = _fabrica.CreateClient();
    }

    [OneTimeTearDown]
    public async Task LiberarAsync()
    {
        _cliente.Dispose();
        await _fabrica.DisposeAsync();
    }

    [SetUp]
    public void Reiniciar()
    {
        _almacen.Falla = false;
    }

    private static Task<long> CrearOrdenAsync(HttpClient cliente)
    {
        return DatosDePrueba.CrearOrdenIniciadaAsync(cliente);
    }

    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient cliente, long ordenId, string tipo, byte[] archivo,
        string nombreArchivo)
    {
        var contenido = new ByteArrayContent(archivo);
        contenido.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var formulario = new MultipartFormDataContent
        {
            { new StringContent(tipo), "tipo" },
            { new StringContent(FechaDeLaEmpresa.Hoy().ToString("yyyy-MM-dd")), "fechaRegistro" },
            { contenido, "archivo", nombreArchivo },
        };
        return cliente.PostAsync($"/api/v1/ordenes-trabajo/{ordenId}/evidencias", formulario);
    }

    private async Task<(string? Clave, byte[]? Archivo)> LeerFilaAsync(long evidenciaId)
    {
        using var ambito = _fabrica.Services.CreateScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<SotraContext>();
        var fila = await contexto.EvidenciasOt.AsNoTracking().SingleAsync(evidencia => evidencia.IdEvidencia == evidenciaId);
        return (fila.ClaveArchivo, fila.Archivo);
    }

    [Test]
    public async Task UnaFotoSeGuardaEnLaCarpetaEvidenciasYNoEnLaBase()
    {
        var ordenId = await CrearOrdenAsync(_cliente);

        var creada = await RegistrarAsync(_cliente, ordenId, "foto", Png, "frente.png");

        Assert.That(creada.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var evidenciaId = (await creada.LeerJsonAsync()).GetProperty("id").GetInt64();
        var fila = await LeerFilaAsync(evidenciaId);
        Assert.That(fila.Archivo, Is.Null);
        Assert.That(fila.Clave, Does.Match($@"^evidencias/{ordenId}/[0-9a-f]{{32}}\.png$"));
        Assert.That(_almacen.Archivos[fila.Clave!], Is.EqualTo(Png));

        var descarga = await _cliente.GetAsync($"/api/v1/ordenes-trabajo/{ordenId}/evidencias/{evidenciaId}/archivo");
        Assert.That(descarga.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(descarga.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"));
        Assert.That(descarga.Content.Headers.ContentDisposition?.FileName, Does.Contain("frente.png"));
        Assert.That(await descarga.Content.ReadAsByteArrayAsync(), Is.EqualTo(Png));
    }

    [Test]
    public async Task UnDocumentoPdfTambienVaAlAlmacen()
    {
        var ordenId = await CrearOrdenAsync(_cliente);

        var creada = await RegistrarAsync(_cliente, ordenId, "documento", Pdf, "informe.pdf");

        Assert.That(creada.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var fila = await LeerFilaAsync((await creada.LeerJsonAsync()).GetProperty("id").GetInt64());
        Assert.That(fila.Clave, Does.StartWith($"evidencias/{ordenId}/").And.EndWith(".pdf"));
    }

    [Test]
    public async Task SiElAlmacenFallaLaEvidenciaNoSeRegistraYSeResponde503()
    {
        var ordenId = await CrearOrdenAsync(_cliente);
        _almacen.Falla = true;

        var respuesta = await RegistrarAsync(_cliente, ordenId, "foto", Png, "frente.png");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo("ALMACEN_NO_DISPONIBLE"));
        var listado = await (await _cliente.GetAsync($"/api/v1/ordenes-trabajo/{ordenId}/evidencias")).LeerJsonAsync();
        Assert.That(listado.GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task SiElArchivoNoEstaEnElAlmacenLaDescargaResponde404()
    {
        var ordenId = await CrearOrdenAsync(_cliente);
        var evidenciaId = (await (await RegistrarAsync(_cliente, ordenId, "foto", Png, "frente.png")).LeerJsonAsync())
            .GetProperty("id").GetInt64();
        _almacen.Archivos.TryRemove((await LeerFilaAsync(evidenciaId)).Clave!, out _);

        var descarga = await _cliente.GetAsync($"/api/v1/ordenes-trabajo/{ordenId}/evidencias/{evidenciaId}/archivo");

        Assert.That(descarga.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task LasEvidenciasQueQuedaronEnLaBaseSePasanAlAlmacenYSeSiguenDescargando()
    {
        // Evidencia guardada en la base, como las de antes de R2.
        using var clienteSinR2 = EntornoDePruebas.Fabrica.CreateClient();
        var ordenId = await CrearOrdenAsync(clienteSinR2);
        var evidenciaId = (await (await RegistrarAsync(clienteSinR2, ordenId, "foto", Png, "vieja.png")).LeerJsonAsync())
            .GetProperty("id").GetInt64();
        Assert.That((await LeerFilaAsync(evidenciaId)).Archivo, Is.EqualTo(Png));

        var migrador = new MigradorDeArchivosAlAlmacen(_fabrica.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MigradorDeArchivosAlAlmacen>.Instance);
        await migrador.StartAsync(CancellationToken.None);
        await migrador.ExecuteTask!;

        var fila = await LeerFilaAsync(evidenciaId);
        Assert.That(fila.Archivo, Is.Null);
        Assert.That(fila.Clave, Does.StartWith($"evidencias/{ordenId}/"));
        Assert.That(_almacen.Archivos[fila.Clave!], Is.EqualTo(Png));
        var descarga = await _cliente.GetAsync($"/api/v1/ordenes-trabajo/{ordenId}/evidencias/{evidenciaId}/archivo");
        Assert.That(await descarga.Content.ReadAsByteArrayAsync(), Is.EqualTo(Png));
    }
}
