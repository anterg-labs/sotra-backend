using System.Text.Json;
using System.Text.RegularExpressions;
using Api.Infraestructura;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

/// <summary>
/// El documento OpenAPI es el contrato público de la API: estas pruebas hacen que no se pueda agregar un endpoint sin
/// documentarlo ni cambiar su seguridad sin que se note.
/// </summary>
public class OpenApiPruebas : PruebaBase
{
    private static readonly string[] Metodos = ["get", "post", "put", "patch", "delete"];

    private async Task<JsonElement> LeerDocumentoAsync()
    {
        var respuesta = await Cliente.GetAsync("/openapi/v1.json");
        Assert.That(respuesta.IsSuccessStatusCode, Is.True);
        return await respuesta.LeerJsonAsync();
    }

    private static IEnumerable<(string Ruta, string Metodo, JsonElement Operacion)> Operaciones(JsonElement documento)
    {
        foreach (var ruta in documento.GetProperty("paths").EnumerateObject())
        {
            foreach (var metodo in ruta.Value.EnumerateObject().Where(m => Metodos.Contains(m.Name)))
            {
                yield return (ruta.Name, metodo.Name.ToUpperInvariant(), metodo.Value);
            }
        }
    }

    [Test]
    public async Task ElDocumentoDeclaraTituloVersionEsquemaBearerYEtiquetas()
    {
        var documento = await LeerDocumentoAsync();

        Assert.That(documento.GetProperty("info").GetProperty("title").GetString(), Is.EqualTo("SOTRA API"));
        Assert.That(documento.GetProperty("info").GetProperty("version").GetString(), Does.Match(@"^\d+\.\d+\.\d+$"));
        var bearer = documento.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.That(bearer.GetProperty("type").GetString(), Is.EqualTo("http"));
        Assert.That(bearer.GetProperty("scheme").GetString(), Is.EqualTo("bearer"));
        Assert.That(documento.GetProperty("tags").GetArrayLength(), Is.GreaterThanOrEqualTo(17));
        Assert.That(documento.GetProperty("components").GetProperty("schemas").TryGetProperty("ErrorApi", out _), Is.True);
    }

    [Test]
    public async Task SoloLaVersionEsAnonimaYElRestoExigeTokenBearer()
    {
        var documento = await LeerDocumentoAsync();

        foreach (var (ruta, metodo, operacion) in Operaciones(documento))
        {
            var exigeToken = operacion.TryGetProperty("security", out var seguridad) && seguridad.GetArrayLength() > 0;
            Assert.That(exigeToken, Is.EqualTo(ruta != "/api/v1/version"), $"{metodo} {ruta}");
        }
    }

    [Test]
    public async Task TodaOperacionTieneResumenEtiquetaYLasRespuestasDeErrorComunes()
    {
        var documento = await LeerDocumentoAsync();

        foreach (var (ruta, metodo, operacion) in Operaciones(documento))
        {
            var nombre = $"{metodo} {ruta}";
            Assert.That(operacion.TryGetProperty("summary", out var resumen) || operacion.TryGetProperty("description", out resumen), Is.True, nombre + ": sin resumen");
            Assert.That(resumen.GetString(), Is.Not.Empty, nombre + ": resumen vacío");
            Assert.That(operacion.GetProperty("tags").GetArrayLength(), Is.GreaterThan(0), nombre + ": sin etiqueta");

            var respuestas = operacion.GetProperty("responses");
            Assert.That(respuestas.TryGetProperty("429", out _), Is.True, nombre + ": sin 429");
            Assert.That(respuestas.TryGetProperty("500", out _), Is.True, nombre + ": sin 500");
            if (ruta != "/api/v1/version")
            {
                Assert.That(respuestas.TryGetProperty("401", out _), Is.True, nombre + ": sin 401");
                Assert.That(respuestas.TryGetProperty("403", out _), Is.True, nombre + ": sin 403");
            }
        }
    }

    [Test]
    public async Task ElDocumentoTieneUnaOperacionPorCadaEndpointDeLosControladores()
    {
        var documento = await LeerDocumentoAsync();
        var documentadas = Operaciones(documento).Select(o => $"{o.Metodo} {o.Ruta}").ToHashSet();

        var fuente = EntornoDePruebas.Fabrica.Services.GetRequiredService<EndpointDataSource>();
        var reales = fuente.Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("api/v1") == true)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? []).Select(m => $"{m} /{Regex.Replace(e.RoutePattern.RawText!, @"\{(\w+):[^}]+\}", "{$1}")}"))
            .ToHashSet();

        Assert.That(reales, Is.Not.Empty);
        Assert.That(documentadas.Except(reales), Is.Empty, "operaciones del documento que ya no existen");
        Assert.That(reales.Except(documentadas), Is.Empty, "endpoints que no están en el documento");
    }

    [Test]
    public async Task ElDocumentoEsConsistenteTodasLasReferenciasSeResuelven()
    {
        var documento = await LeerDocumentoAsync();
        var esquemas = documento.GetProperty("components").GetProperty("schemas");
        Assert.That(documento.GetProperty("openapi").GetString(), Does.StartWith("3."));

        var referencias = new List<string>();
        Recorrer(documento, referencias);
        Assert.That(referencias, Is.Not.Empty);
        foreach (var referencia in referencias.Distinct())
        {
            var prefijo = "#/components/schemas/";
            Assert.That(referencia, Does.StartWith(prefijo), referencia);
            Assert.That(esquemas.TryGetProperty(referencia[prefijo.Length..], out _), Is.True, "esquema inexistente: " + referencia);
        }
    }

    private static void Recorrer(JsonElement elemento, List<string> referencias)
    {
        switch (elemento.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var propiedad in elemento.EnumerateObject())
                {
                    if (propiedad.Name == "$ref" && propiedad.Value.ValueKind == JsonValueKind.String)
                    {
                        referencias.Add(propiedad.Value.GetString()!);
                    }
                    else
                    {
                        Recorrer(propiedad.Value, referencias);
                    }
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in elemento.EnumerateArray())
                {
                    Recorrer(item, referencias);
                }

                break;
        }
    }

    [Test]
    public async Task LaInterfazDeSwaggerSeSirveEnDesarrolloConSuPropiaPoliticaDeContenido()
    {
        var interfaz = await Cliente.GetAsync("/swagger/index.html");
        Assert.That(interfaz.IsSuccessStatusCode, Is.True);
        Assert.That(interfaz.Headers.GetValues("Content-Security-Policy").Single(), Does.Contain("script-src 'self' 'unsafe-inline'"));

        // El resto de la API conserva la política estricta.
        var api = await Cliente.GetAsync("/api/v1/version");
        Assert.That(api.Headers.GetValues("Content-Security-Policy").Single(), Is.EqualTo("default-src 'none'; frame-ancestors 'none'"));
    }

    [TestCase("Development", null, true)]
    [TestCase("Production", null, false)]
    [TestCase("Production", "false", false)]
    [TestCase("Production", "true", true)]
    [TestCase("Staging", "TRUE", true)]
    public void ElDocumentoYLaInterfazSoloSePublicanEnDesarrolloOConLaVariableExplicita(string entorno, string? variable, bool publica)
    {
        var anterior = Environment.GetEnvironmentVariable("OPENAPI_PUBLICAR");
        try
        {
            Environment.SetEnvironmentVariable("OPENAPI_PUBLICAR", variable);
            Assert.That(DocumentacionOpenApi.DebePublicarse(new EntornoFalso(entorno)), Is.EqualTo(publica));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OPENAPI_PUBLICAR", anterior);
        }
    }

    private sealed class EntornoFalso(string nombre) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = nombre;

        public string ApplicationName { get; set; } = "Api";

        public string ContentRootPath { get; set; } = string.Empty;

        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Test]
    public async Task LosFlujosPrincipalesTienenEjemplosYLaConcurrenciaDeOrdenesEstaDocumentada()
    {
        var documento = await LeerDocumentoAsync();
        var rutas = documento.GetProperty("paths");

        Assert.That(rutas.GetProperty("/api/v1/ordenes-trabajo").GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").TryGetProperty("example", out _), Is.True);
        Assert.That(rutas.GetProperty("/api/v1/ordenes-trabajo/{id}/estado").GetProperty("patch").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").TryGetProperty("example", out _), Is.True);
        Assert.That(rutas.GetProperty("/api/v1/repuestos/{id}/ingresos").GetProperty("post").GetProperty("requestBody")
            .GetProperty("content").GetProperty("application/json").TryGetProperty("example", out _), Is.True);

        var modificar = rutas.GetProperty("/api/v1/ordenes-trabajo/{id}").GetProperty("put");
        Assert.That(modificar.GetProperty("parameters").EnumerateArray().Any(p => p.GetProperty("name").GetString() == "If-Match"), Is.True);
        Assert.That(modificar.GetProperty("responses").TryGetProperty("412", out _), Is.True);
        Assert.That(modificar.GetProperty("responses").GetProperty("200").GetProperty("headers").TryGetProperty("ETag", out _), Is.True);
        Assert.That(rutas.GetProperty("/api/v1/ordenes-trabajo/{id}").GetProperty("get").GetProperty("responses")
            .GetProperty("200").GetProperty("headers").TryGetProperty("ETag", out _), Is.True);
    }

    [Test]
    public async Task CadaOperacionTieneUnIdentificadorUnico()
    {
        var documento = await LeerDocumentoAsync();
        var identificadores = Operaciones(documento).Select(o => o.Operacion.GetProperty("operationId").GetString()!).ToList();

        Assert.That(identificadores, Is.Unique);
        Assert.That(identificadores, Has.All.Match(@"^[a-z][A-Za-z]*_[a-z][A-Za-z]*$"));
    }

    /// <summary>La integración continua define <c>OPENAPI_SALIDA</c> para guardar el documento y revisarlo con Spectral.</summary>
    [Test]
    public async Task ElDocumentoSeExportaSiSeIndicaUnaSalida()
    {
        var salida = Environment.GetEnvironmentVariable("OPENAPI_SALIDA");
        if (string.IsNullOrWhiteSpace(salida))
        {
            Assert.Ignore("OPENAPI_SALIDA no está definida: solo se exporta en la integración continua.");
        }

        var respuesta = await Cliente.GetAsync("/openapi/v1.json");
        await File.WriteAllTextAsync(salida!, await respuesta.Content.ReadAsStringAsync());
        Assert.That(new FileInfo(salida!).Length, Is.GreaterThan(1000));
    }
}
