using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Api.Servicios;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Api.Pruebas.Caracterizacion;

/// <summary>
/// La subida de un documento para las sugerencias de IA responde enseguida y el procesamiento corre en segundo plano.
/// Usa embeddings de prueba: los modelos ONNX no están en el entorno de pruebas.
/// </summary>
public class ProcesamientoDeDocumentosPruebas
{
    private static readonly TimeSpan EsperaMaxima = TimeSpan.FromSeconds(30);
    private static readonly byte[] PdfSinTexto = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();

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
                servicios.AddSingleton<ServicioEmbeddings, EmbeddingsDePrueba>()));
        _cliente = _fabrica.CreateClient();
    }

    [OneTimeTearDown]
    public async Task LiberarAsync()
    {
        _cliente.Dispose();
        await _fabrica.DisposeAsync();
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

    private async Task<JsonElement> SubirAsync(byte[] contenido, string nombre)
    {
        var formulario = new MultipartFormDataContent { { new StringContent("true"), "usaSugerenciasIA" } };
        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formulario.Add(archivo, "archivo", nombre);
        var respuesta = await _cliente.PostAsync("/api/v1/documentos-tecnicos", formulario);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        return await respuesta.LeerJsonAsync();
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

    [Test]
    public async Task LaSubidaRespondeAntesDeProcesarYElDocumentoQuedaListoConSusFragmentos()
    {
        var creado = await SubirAsync(CrearPdfConTexto(), "aceite-" + DatosDePrueba.Sufijo() + ".pdf");

        Assert.That(creado.GetProperty("estadoProcesamiento").GetString(),
            Is.AnyOf(EstadosDeProcesamiento.Pendiente, EstadosDeProcesamiento.Procesando, EstadosDeProcesamiento.Listo));
        var documento = await EsperarFinDelProcesamientoAsync(creado.GetProperty("id").GetInt64());

        Assert.That(documento.GetProperty("estadoProcesamiento").GetString(), Is.EqualTo(EstadosDeProcesamiento.Listo));
        Assert.That(documento.GetProperty("cantidadFragmentos").GetInt32(), Is.GreaterThan(0));
        Assert.That(documento.GetProperty("codigoErrorProcesamiento").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task UnPdfSinTextoQuedaGuardadoConElErrorDelProcesamiento()
    {
        var creado = await SubirAsync(PdfSinTexto, "sin-texto-" + DatosDePrueba.Sufijo() + ".pdf");

        var documento = await EsperarFinDelProcesamientoAsync(creado.GetProperty("id").GetInt64());

        Assert.That(documento.GetProperty("estadoProcesamiento").GetString(), Is.EqualTo(EstadosDeProcesamiento.Error));
        Assert.That(documento.GetProperty("codigoErrorProcesamiento").GetString(),
            Is.AnyOf(ErroresDeProcesamiento.PdfInvalido, ErroresDeProcesamiento.PdfSinTexto));
        Assert.That(documento.GetProperty("mensajeErrorProcesamiento").GetString(), Is.Not.Empty);
        Assert.That(documento.GetProperty("cantidadFragmentos").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task ReprocesarRespondeAceptadoYElDocumentoVuelveAQuedarListo()
    {
        var creado = await SubirAsync(CrearPdfConTexto(), "reprocesar-" + DatosDePrueba.Sufijo() + ".pdf");
        var id = creado.GetProperty("id").GetInt64();
        var listo = await EsperarFinDelProcesamientoAsync(id);
        Assert.That(listo.GetProperty("estadoProcesamiento").GetString(), Is.EqualTo(EstadosDeProcesamiento.Listo));

        var respuesta = await _cliente.PostAsync($"/api/v1/documentos-tecnicos/{id}/reprocesar", null);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Accepted));
        var documento = await EsperarFinDelProcesamientoAsync(id);
        Assert.That(documento.GetProperty("estadoProcesamiento").GetString(), Is.EqualTo(EstadosDeProcesamiento.Listo));
        Assert.That(documento.GetProperty("cantidadFragmentos").GetInt32(),
            Is.EqualTo(listo.GetProperty("cantidadFragmentos").GetInt32()));
    }

    [Test]
    public async Task UnDocumentoBorradoMientrasEsperaNoDejaFragmentosSueltos()
    {
        var nombre = "borrado-" + DatosDePrueba.Sufijo() + ".pdf";
        var creado = await SubirAsync(CrearPdfConTexto(), nombre);
        var id = creado.GetProperty("id").GetInt64();

        Assert.That((await _cliente.DeleteAsync($"/api/v1/documentos-tecnicos/{id}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NoContent));

        // La cola procesa de a uno y en orden: cuando el siguiente está listo, el borrado ya se resolvió.
        var siguiente = await SubirAsync(CrearPdfConTexto(), "despues-" + DatosDePrueba.Sufijo() + ".pdf");
        await EsperarFinDelProcesamientoAsync(siguiente.GetProperty("id").GetInt64());
        Assert.That((await _cliente.GetAsync($"/api/v1/documentos-tecnicos/{id}")).StatusCode,
            Is.EqualTo(HttpStatusCode.NotFound));
        await using var baseDatos = BaseVectorial.CrearConexion();
        await using var contar = baseDatos.CreateCommand("SELECT count(*) FROM vectorial.documentos WHERE nombre = $1");
        contar.Parameters.AddWithValue(nombre);
        Assert.That(Convert.ToInt64(await contar.ExecuteScalarAsync()), Is.EqualTo(0));
    }
}
