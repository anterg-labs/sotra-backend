using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Api.Servicios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Api.Pruebas.Caracterizacion;

/// <summary>
/// Con R2 configurado, los PDF de los documentos técnicos van a documentacion/ y no a la base; los fragmentos y
/// embeddings siguen en la base. Usa un almacén en memoria en lugar del bucket real y embeddings de prueba.
/// </summary>
public class DocumentosTecnicosEnR2Pruebas
{
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(30);
    private static readonly byte[] PdfSinTexto = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();

    private readonly AlmacenEnMemoria _almacen = new();
    private WebApplicationFactory<PuntoDeEntrada> _fabrica = null!;
    private HttpClient _cliente = null!;

    private sealed class EmbeddingsDePrueba : ServicioEmbeddings
    {
        public override void VerificarDisponible()
        {
        }

        public override List<float[]> Crear(List<string> textos)
        {
            return textos.Select(texto =>
            {
                var vector = new float[DimensionesDelEmbedding];
                vector[Math.Abs(texto.GetHashCode()) % DimensionesDelEmbedding] = 1;
                return vector;
            }).ToList();
        }
    }

    [OneTimeSetUp]
    public void Preparar()
    {
        _fabrica = EntornoDePruebas.Fabrica.WithWebHostBuilder(constructor =>
            constructor.ConfigureTestServices(servicios =>
            {
                servicios.AddSingleton<ServicioEmbeddings, EmbeddingsDePrueba>();
                servicios.AddSingleton<IAlmacenDeArchivos>(_almacen);
            }));
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

    private static byte[] CrearPdfConTexto()
    {
        var constructor = new PdfDocumentBuilder();
        var fuente = constructor.AddStandard14Font(Standard14Font.Helvetica);
        var pagina = constructor.AddPage(PageSize.A4);
        string[] lineas =
        [
            "4.3 Cambio de aceite del motor",
            "El aceite del motor se cambia cada 250 horas de trabajo con aceite 15W-40.",
            "Detenga el motor, retire el tapon de drenaje y deje escurrir el aceite usado.",
            "Coloque el filtro nuevo y llene el carter con 14 litros de aceite.",
        ];
        for (var posicion = 0; posicion < lineas.Length; posicion++)
        {
            pagina.AddText(lineas[posicion], 11, new PdfPoint(40, 780 - posicion * 20), fuente);
        }

        return constructor.Build();
    }

    private static Task<HttpResponseMessage> SubirAsync(HttpClient cliente, byte[] contenido, string nombre,
        bool usaSugerenciasIa)
    {
        var formulario = new MultipartFormDataContent
        {
            { new StringContent(usaSugerenciasIa ? "true" : "false"), "usaSugerenciasIA" },
        };
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formulario.Add(archivo, "archivo", nombre);
        return cliente.PostAsync("/api/v1/documentos-tecnicos", formulario);
    }

    private static async Task<long> SubirYObtenerIdAsync(HttpClient cliente, byte[] contenido, bool usaSugerenciasIa)
    {
        var respuesta = await SubirAsync(cliente, contenido, "manual-" + DatosDePrueba.Sufijo() + ".pdf", usaSugerenciasIa);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private async Task<JsonElement> EsperarFinDelProcesamientoAsync(long id)
    {
        var limite = DateTime.UtcNow + EsperaMaxima;
        while (true)
        {
            var documento = await (await _cliente.GetAsync($"/api/v1/documentos-tecnicos/{id}")).LeerJsonAsync();
            var estado = documento.GetProperty("estadoProcesamiento").GetString();
            if (estado is EstadosDeProcesamiento.Listo or EstadosDeProcesamiento.Error || DateTime.UtcNow > limite)
            {
                return documento;
            }

            await Task.Delay(200);
        }
    }

    private static async Task<(string? Clave, bool TieneArchivoEnLaBase)> LeerFilaAsync(long id)
    {
        await using var baseDatos = BaseVectorial.CrearConexion();
        await using var comando = baseDatos.CreateCommand(
            "SELECT clave_archivo, archivo IS NOT NULL FROM vectorial.documento_tecnico WHERE id = $1");
        comando.Parameters.AddWithValue(id);
        await using var lector = await comando.ExecuteReaderAsync();
        Assert.That(await lector.ReadAsync(), Is.True, "El documento no está en la base.");
        return (lector.IsDBNull(0) ? null : lector.GetString(0), lector.GetBoolean(1));
    }

    private static async Task<int> ContarPorNombreAsync(string nombre)
    {
        await using var baseDatos = BaseVectorial.CrearConexion();
        await using var comando = baseDatos.CreateCommand(
            "SELECT count(*) FROM vectorial.documento_tecnico WHERE nombre_archivo = $1");
        comando.Parameters.AddWithValue(nombre);
        return Convert.ToInt32(await comando.ExecuteScalarAsync());
    }

    [Test]
    public async Task ElPdfSeGuardaEnDocumentacionYNoEnLaBaseYSeDescargaIgual()
    {
        var pdf = CrearPdfConTexto();

        var id = await SubirYObtenerIdAsync(_cliente, pdf, usaSugerenciasIa: false);

        var fila = await LeerFilaAsync(id);
        Assert.That(fila.TieneArchivoEnLaBase, Is.False);
        Assert.That(fila.Clave, Does.Match("^documentacion/[0-9a-f]{32}\\.pdf$"));
        Assert.That(_almacen.Archivos[fila.Clave!], Is.EqualTo(pdf));
        var descarga = await _cliente.GetAsync($"/api/v1/documentos-tecnicos/{id}/archivo");
        Assert.That(descarga.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(descarga.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/pdf"));
        Assert.That(await descarga.Content.ReadAsByteArrayAsync(), Is.EqualTo(pdf));
    }

    [Test]
    public async Task ElProcesamientoParaLaIaLeeElPdfDeR2YDejaLosFragmentosEnLaBase()
    {
        var id = await SubirYObtenerIdAsync(_cliente, CrearPdfConTexto(), usaSugerenciasIa: true);

        var documento = await EsperarFinDelProcesamientoAsync(id);

        Assert.That(documento.GetProperty("estadoProcesamiento").GetString(), Is.EqualTo(EstadosDeProcesamiento.Listo));
        Assert.That(documento.GetProperty("cantidadFragmentos").GetInt32(), Is.GreaterThan(0));
        Assert.That((await LeerFilaAsync(id)).TieneArchivoEnLaBase, Is.False);

        var reprocesar = await _cliente.PostAsync($"/api/v1/documentos-tecnicos/{id}/reprocesar", null);
        Assert.That(reprocesar.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        var reprocesado = await EsperarFinDelProcesamientoAsync(id);
        Assert.That(reprocesado.GetProperty("estadoProcesamiento").GetString(), Is.EqualTo(EstadosDeProcesamiento.Listo));
        Assert.That(reprocesado.GetProperty("cantidadFragmentos").GetInt32(),
            Is.EqualTo(documento.GetProperty("cantidadFragmentos").GetInt32()));
    }

    [Test]
    public async Task SiElPdfNoEstaEnR2ElProcesamientoQuedaConError()
    {
        var id = await SubirYObtenerIdAsync(_cliente, CrearPdfConTexto(), usaSugerenciasIa: true);
        await EsperarFinDelProcesamientoAsync(id);
        _almacen.Archivos.TryRemove((await LeerFilaAsync(id)).Clave!, out _);

        await _cliente.PostAsync($"/api/v1/documentos-tecnicos/{id}/reprocesar", null);
        var documento = await EsperarFinDelProcesamientoAsync(id);

        Assert.That(documento.GetProperty("estadoProcesamiento").GetString(), Is.EqualTo(EstadosDeProcesamiento.Error));
        Assert.That(documento.GetProperty("codigoErrorProcesamiento").GetString(),
            Is.EqualTo(ErroresDeProcesamiento.ArchivoNoDisponible));
    }

    [Test]
    public async Task SiR2FallaLaSubidaResponde503YElDocumentoNoSeRegistra()
    {
        var nombre = "caido-" + DatosDePrueba.Sufijo() + ".pdf";
        _almacen.Falla = true;

        var respuesta = await SubirAsync(_cliente, PdfSinTexto, nombre, usaSugerenciasIa: false);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo("ALMACEN_NO_DISPONIBLE"));
        Assert.That(await ContarPorNombreAsync(nombre), Is.Zero);
    }

    [Test]
    public async Task AlEliminarElDocumentoSeBorraSuPdfDeR2()
    {
        var id = await SubirYObtenerIdAsync(_cliente, PdfSinTexto, usaSugerenciasIa: false);
        var clave = (await LeerFilaAsync(id)).Clave!;

        var borrado = await _cliente.DeleteAsync($"/api/v1/documentos-tecnicos/{id}");

        Assert.That(borrado.IsSuccessStatusCode, Is.True);
        Assert.That(_almacen.Archivos.ContainsKey(clave), Is.False);
    }

    [Test]
    public async Task LosPdfQueQuedaronEnLaBaseSePasanAR2YSeSiguenDescargando()
    {
        // Documento guardado en la base, como los de antes de R2.
        using var clienteSinR2 = EntornoDePruebas.Fabrica.CreateClient();
        var id = await SubirYObtenerIdAsync(clienteSinR2, PdfSinTexto, usaSugerenciasIa: false);
        Assert.That((await LeerFilaAsync(id)).TieneArchivoEnLaBase, Is.True);

        var migrador = new MigradorDeArchivosAlAlmacen(_fabrica.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<MigradorDeArchivosAlAlmacen>.Instance);
        await migrador.StartAsync(CancellationToken.None);
        await migrador.ExecuteTask!;

        var fila = await LeerFilaAsync(id);
        Assert.That(fila.TieneArchivoEnLaBase, Is.False);
        Assert.That(fila.Clave, Does.StartWith("documentacion/"));
        Assert.That(_almacen.Archivos[fila.Clave!], Is.EqualTo(PdfSinTexto));
        var descarga = await _cliente.GetAsync($"/api/v1/documentos-tecnicos/{id}/archivo");
        Assert.That(await descarga.Content.ReadAsByteArrayAsync(), Is.EqualTo(PdfSinTexto));
    }
}
