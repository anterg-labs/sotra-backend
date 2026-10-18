using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class EvidenciasPruebas : PruebaBase
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];
    private static readonly byte[] Pdf = "%PDF-1.4\n%%EOF"u8.ToArray();
    private static readonly byte[] Ejecutable = [0x4D, 0x5A, 0x90, 0x00, 0x03];

    private async Task<long> CrearOrdenAsync()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo = "correctivo",
            prioridad = "baja",
            descripcion = "Revisión general",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow),
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private Task<HttpResponseMessage> RegistrarAsync(long ordenId, string tipo, byte[]? archivo = null,
        string nombreArchivo = "evidencia.png", string? observacion = "Detalle", string? fecha = null)
    {
        var formulario = new MultipartFormDataContent
        {
            { new StringContent(tipo), "tipo" },
            { new StringContent(fecha ?? DateTime.UtcNow.ToString("yyyy-MM-dd")), "fechaRegistro" },
        };
        if (observacion is not null)
        {
            formulario.Add(new StringContent(observacion), "observacion");
        }

        if (archivo is not null)
        {
            var contenido = new ByteArrayContent(archivo);
            contenido.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            formulario.Add(contenido, "archivo", nombreArchivo);
        }

        return Cliente.PostAsync($"/api/v1/ordenes-trabajo/{ordenId}/evidencias", formulario);
    }

    private static async Task<string?> LeerCodigoAsync(HttpResponseMessage respuesta)
    {
        return (await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString();
    }

    [Test]
    public async Task UnaObservacionSinArchivoSeRegistra()
    {
        var ordenId = await CrearOrdenAsync();

        var respuesta = await RegistrarAsync(ordenId, "observacion", observacion: "Se limpió el radiador");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var evidencia = await respuesta.LeerJsonAsync();
        Assert.That(evidencia.GetProperty("tieneArchivo").GetBoolean(), Is.False);
    }

    [Test]
    public async Task UnaFotoPngSeGuardaYSeDescargaIgualConCabecerasSeguras()
    {
        var ordenId = await CrearOrdenAsync();

        var creada = await RegistrarAsync(ordenId, "foto", Png);

        Assert.That(creada.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var evidencia = await creada.LeerJsonAsync();
        Assert.That(evidencia.GetProperty("tipoContenido").GetString(), Is.EqualTo("image/png"));

        var descarga = await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{ordenId}/evidencias/{evidencia.GetProperty("id").GetInt64()}/archivo");
        Assert.That(descarga.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(descarga.Content.Headers.ContentType?.MediaType, Is.EqualTo("image/png"));
        Assert.That(await descarga.Content.ReadAsByteArrayAsync(), Is.EqualTo(Png));
        Assert.That(descarga.Headers.GetValues("X-Content-Type-Options"), Does.Contain("nosniff"));
    }

    [Test]
    public async Task ElTipoRealSeDecidePorElContenidoYNoPorElNombre()
    {
        var ordenId = await CrearOrdenAsync();

        var ejecutableConNombreDeImagen = await RegistrarAsync(ordenId, "foto", Ejecutable, "inocente.png");
        var pdfComoFoto = await RegistrarAsync(ordenId, "foto", Pdf, "manual.png");

        Assert.That(ejecutableConNombreDeImagen.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(ejecutableConNombreDeImagen), Is.EqualTo("ARCHIVO_NO_PERMITIDO"));
        Assert.That(pdfComoFoto.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task UnDocumentoPuedeSerPdf()
    {
        var ordenId = await CrearOrdenAsync();

        var respuesta = await RegistrarAsync(ordenId, "documento", Pdf, "informe.pdf");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task ElNombreDelArchivoSeGuardaSinRutas()
    {
        var ordenId = await CrearOrdenAsync();

        var respuesta = await RegistrarAsync(ordenId, "foto", Png, "../../etc/passwd.png");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("nombreArchivo").GetString(), Is.EqualTo("passwd.png"));
    }

    [Test]
    public async Task UnArchivoDeMasDeDiezMegabytesResponde413()
    {
        var ordenId = await CrearOrdenAsync();
        var grande = new byte[10 * 1024 * 1024 + 1];
        Png.CopyTo(grande, 0);

        var respuesta = await RegistrarAsync(ordenId, "foto", grande);

        Assert.That(respuesta.StatusCode, Is.EqualTo((HttpStatusCode)413));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("ARCHIVO_DEMASIADO_GRANDE"));
    }

    [Test]
    public async Task LosDatosInvalidosSeRechazanConCodigoEstable()
    {
        var ordenId = await CrearOrdenAsync();
        var manana = DateTime.UtcNow.AddDays(5).ToString("yyyy-MM-dd");

        var tipoInvalido = await RegistrarAsync(ordenId, "video", Png);
        var fotoSinArchivo = await RegistrarAsync(ordenId, "foto");
        var observacionVacia = await RegistrarAsync(ordenId, "observacion", observacion: "   ");
        var fechaFutura = await RegistrarAsync(ordenId, "observacion", fecha: manana);

        Assert.That(await LeerCodigoAsync(tipoInvalido), Is.EqualTo("TIPO_EVIDENCIA_INVALIDO"));
        Assert.That(await LeerCodigoAsync(fotoSinArchivo), Is.EqualTo("ARCHIVO_OBLIGATORIO"));
        Assert.That(await LeerCodigoAsync(observacionVacia), Is.EqualTo("OBSERVACION_OBLIGATORIA"));
        Assert.That(await LeerCodigoAsync(fechaFutura), Is.EqualTo("FECHA_REGISTRO_INVALIDA"));
    }

    [Test]
    public async Task UnaOrdenCanceladaNoAdmiteEvidenciasNiLasDeOtraOrdenSeMezclan()
    {
        var cancelada = await CrearOrdenAsync();
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{cancelada}/estado",
            new { estado = "cancelada", motivo = "Duplicada" })).EnsureSuccessStatusCode();
        var otra = await CrearOrdenAsync();
        var propia = await (await RegistrarAsync(otra, "foto", Png)).LeerJsonAsync();

        var enCancelada = await RegistrarAsync(cancelada, "observacion");
        var descargaCruzada = await Cliente.GetAsync(
            $"/api/v1/ordenes-trabajo/{cancelada}/evidencias/{propia.GetProperty("id").GetInt64()}/archivo");

        Assert.That(enCancelada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(enCancelada), Is.EqualTo("ORDEN_NO_MODIFICABLE"));
        Assert.That(descargaCruzada.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task EnUnaOrdenInexistenteSeResponde404()
    {
        var respuesta = await RegistrarAsync(long.MaxValue, "observacion");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{long.MaxValue}/evidencias")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
