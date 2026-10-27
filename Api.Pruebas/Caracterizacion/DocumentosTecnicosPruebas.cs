using System.Net;
using System.Net.Http.Headers;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class DocumentosTecnicosPruebas : PruebaBase
{
    private static readonly byte[] Pdf = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();

    private Task<HttpResponseMessage> SubirAsync(byte[] contenido, string nombre = "manual.pdf",
        bool usaSugerenciasIa = false, long? activoId = null)
    {
        var formulario = new MultipartFormDataContent { { new StringContent(usaSugerenciasIa ? "true" : "false"), "usaSugerenciasIA" } };
        if (activoId is not null)
        {
            formulario.Add(new StringContent(activoId.Value.ToString()), "activoId");
        }

        var archivo = new ByteArrayContent(contenido);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        formulario.Add(archivo, "archivo", nombre);
        return Cliente.PostAsync("/api/v1/documentos-tecnicos", formulario);
    }

    private static async Task<string?> LeerCodigoAsync(HttpResponseMessage respuesta)
    {
        return (await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString();
    }

    [Test]
    public async Task UnPdfSinSugerenciasSeGuardaSeListaSeDescargaIgualYSeElimina()
    {
        var nombre = "manual-" + DatosDePrueba.Sufijo() + ".pdf";

        var creado = await SubirAsync(Pdf, nombre);

        Assert.That(creado.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var documento = await creado.LeerJsonAsync();
        var id = documento.GetProperty("id").GetInt64();
        Assert.That(documento.GetProperty("usaSugerenciasIA").GetBoolean(), Is.False);
        Assert.That(documento.GetProperty("cantidadFragmentos").GetInt32(), Is.EqualTo(0));
        Assert.That(documento.GetProperty("estadoProcesamiento").GetString(), Is.EqualTo("listo"));

        var detalle = await Cliente.GetAsync($"/api/v1/documentos-tecnicos/{id}");
        Assert.That(detalle.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await detalle.LeerJsonAsync()).GetProperty("nombreArchivo").GetString(), Is.EqualTo(nombre));

        var descarga = await Cliente.GetAsync($"/api/v1/documentos-tecnicos/{id}/archivo");
        Assert.That(descarga.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/pdf"));
        Assert.That(await descarga.Content.ReadAsByteArrayAsync(), Is.EqualTo(Pdf));

        Assert.That((await Cliente.DeleteAsync($"/api/v1/documentos-tecnicos/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
        Assert.That((await Cliente.GetAsync($"/api/v1/documentos-tecnicos/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await Cliente.DeleteAsync($"/api/v1/documentos-tecnicos/{id}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task UnArchivoQueNoEsPdfSeRechazaAunqueTengaNombreDePdf()
    {
        var respuesta = await SubirAsync([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00], "inocente.pdf");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("ARCHIVO_NO_ES_PDF"));
    }

    [Test]
    public async Task UnArchivoVacioSeRechaza()
    {
        var respuesta = await SubirAsync([]);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("ARCHIVO_NO_ES_PDF"));
    }

    [Test]
    public async Task UnActivoInexistenteSeRechazaConCodigoEstable()
    {
        var respuesta = await SubirAsync(Pdf, activoId: long.MaxValue);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("ACTIVO_NO_EXISTE"));
    }

    [Test]
    public async Task ElListadoFiltraPorTextoYPorActivoYPaginaEnLaBase()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var activoId = activo.GetProperty("id").GetInt64();
        var marca = "filtro" + DatosDePrueba.Sufijo();
        await SubirAsync(Pdf, marca + "-uno.pdf", activoId: activoId);
        await SubirAsync(Pdf, marca + "-dos.pdf");

        var porTexto = await (await Cliente.GetAsync($"/api/v1/documentos-tecnicos?buscar={marca}&tamanoPagina=1")).LeerJsonAsync();
        var porActivo = await (await Cliente.GetAsync($"/api/v1/documentos-tecnicos?activoId={activoId}&buscar={marca}")).LeerJsonAsync();
        var segundaPagina = await (await Cliente.GetAsync($"/api/v1/documentos-tecnicos?buscar={marca}&tamanoPagina=1&pagina=2")).LeerJsonAsync();

        Assert.That(porTexto.GetProperty("totalElementos").GetInt32(), Is.EqualTo(2));
        Assert.That(porTexto.GetProperty("totalPaginas").GetInt32(), Is.EqualTo(2));
        Assert.That(porTexto.GetProperty("elementos").GetArrayLength(), Is.EqualTo(1));
        Assert.That(porActivo.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
        Assert.That(porActivo.GetProperty("elementos")[0].GetProperty("activoId").GetInt64(), Is.EqualTo(activoId));
        Assert.That(segundaPagina.GetProperty("elementos")[0].GetProperty("nombreArchivo").GetString(), Does.EndWith("-dos.pdf"));
    }

    [Test]
    public async Task OrdenarDocumentosPorTituloUsaAlfabetoEspanol()
    {
        var prefijo = "manual-" + DatosDePrueba.Sufijo() + "-";
        foreach (var nombre in new[] { "Zorro", "Árbol", "Beto", "abeja" })
        {
            var respuesta = await SubirAsync(Pdf, prefijo + nombre + ".pdf");
            respuesta.EnsureSuccessStatusCode();
        }

        var ruta = "/api/v1/documentos-tecnicos?buscar=" + Uri.EscapeDataString(prefijo)
            + "&ordenarPor=titulo&direccionOrden=asc&tamanoPagina=10";
        var pagina = await (await Cliente.GetAsync(ruta)).LeerJsonAsync();
        var nombres = pagina.GetProperty("elementos").EnumerateArray()
            .Select(documento => documento.GetProperty("nombreArchivo").GetString()![prefijo.Length..]).ToArray();

        Assert.That(nombres, Is.EqualTo(new[] { "abeja.pdf", "Árbol.pdf", "Beto.pdf", "Zorro.pdf" }));
    }

    [Test]
    public async Task LosComodinesDeLaBusquedaSeTratanComoTexto()
    {
        var pagina = await (await Cliente.GetAsync("/api/v1/documentos-tecnicos?buscar=%25_%25")).LeerJsonAsync();

        Assert.That(pagina.GetProperty("totalElementos").GetInt32(), Is.EqualTo(0));
    }

    [TestCase("pagina=0")]
    [TestCase("tamanoPagina=0")]
    [TestCase("tamanoPagina=101")]
    public async Task UnaPaginacionInvalidaResponde400(string consulta)
    {
        var respuesta = await Cliente.GetAsync("/api/v1/documentos-tecnicos?" + consulta);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("PAGINACION_INVALIDA"));
    }

    [Test]
    public async Task SinLosModelosLocalesDeIaResponde502SinGuardarElDocumento()
    {
        var nombre = "con-ia-" + DatosDePrueba.Sufijo() + ".pdf";

        var respuesta = await SubirAsync(Pdf, nombre, usaSugerenciasIa: true);

        Assert.That(respuesta.StatusCode, Is.AnyOf(HttpStatusCode.BadGateway, HttpStatusCode.UnprocessableEntity));
        var pagina = await (await Cliente.GetAsync($"/api/v1/documentos-tecnicos?buscar={nombre}")).LeerJsonAsync();
        Assert.That(pagina.GetProperty("totalElementos").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task DescargarOEliminarUnDocumentoInexistenteResponde404()
    {
        Assert.That((await Cliente.GetAsync($"/api/v1/documentos-tecnicos/{long.MaxValue}/archivo")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await Cliente.DeleteAsync($"/api/v1/documentos-tecnicos/{long.MaxValue}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
