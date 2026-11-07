using System.Net;
using System.Text.Json;
using Api.Infraestructura;
using Api.Servicios;
using Microsoft.Extensions.DependencyInjection;

namespace Api.PruebasUnitarias;

public class GeneradorDeTextoPorClaudePruebas
{
    private const string RespuestaDeClaude = """
        {"id":"msg_1","type":"message","role":"assistant","model":"claude-haiku-5-5",
         "content":[{"type":"thinking","thinking":"","signature":"s"},{"type":"text","text":" Hola "}],
         "stop_reason":"end_turn","stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":2}}
        """;

    private const string RespuestaRechazada = """
        {"id":"msg_1","type":"message","role":"assistant","model":"claude-haiku-5-5","content":[],
         "stop_reason":"refusal","stop_sequence":null,"usage":{"input_tokens":10,"output_tokens":0}}
        """;

    private static readonly string[] Variables =
        ["IA_API_PROVEEDOR", "ANTHROPIC_API_KEY", "IA_API_MODELO", "IA_API_ESFUERZO", "IA_API_LLAMADAS_POR_MINUTO"];

    private readonly Dictionary<string, string?> _valoresAnteriores = new();

    [SetUp]
    public void Preparar()
    {
        foreach (var variable in Variables)
        {
            _valoresAnteriores[variable] = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, null);
        }

        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", "clave-de-prueba");
    }

    [TearDown]
    public void Limpiar()
    {
        foreach (var variable in Variables)
        {
            Environment.SetEnvironmentVariable(variable, _valoresAnteriores[variable]);
        }
    }

    private sealed class ManejadorFalso(HttpStatusCode codigo, string cuerpo) : HttpMessageHandler
    {
        public int Llamadas { get; private set; }
        public HttpRequestMessage? UltimaSolicitud { get; private set; }
        public string UltimoCuerpo { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancellationToken)
        {
            Llamadas++;
            UltimaSolicitud = solicitud;
            UltimoCuerpo = solicitud.Content is null ? "" : await solicitud.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(codigo)
            {
                Content = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private static IGeneradorDeTexto Crear(ManejadorFalso manejador)
    {
        var servicios = new ServiceCollection();
        servicios.AgregarGeneradorDeTexto(opciones => opciones.Retry.Delay = TimeSpan.Zero);
        servicios.AddHttpClient<IGeneradorDeTexto, GeneradorDeTextoPorClaude>()
            .ConfigurePrimaryHttpMessageHandler(() => manejador);
        return servicios.BuildServiceProvider().GetRequiredService<IGeneradorDeTexto>();
    }

    [Test]
    public void PorDefectoSeUsaClaudeYConLaVariableElProveedorCompatible()
    {
        var porDefecto = new ServiceCollection();
        porDefecto.AgregarGeneradorDeTexto();
        Assert.That(porDefecto.BuildServiceProvider().GetRequiredService<IGeneradorDeTexto>(),
            Is.TypeOf<GeneradorDeTextoPorClaude>());

        Environment.SetEnvironmentVariable("IA_API_PROVEEDOR", GeneradorDeTextoPorApi.Proveedor);
        var compatible = new ServiceCollection();
        compatible.AgregarGeneradorDeTexto();
        Assert.That(compatible.BuildServiceProvider().GetRequiredService<IGeneradorDeTexto>(),
            Is.TypeOf<GeneradorDeTextoPorApi>());
    }

    [Test]
    public async Task EnviaElPedidoSinTemperaturaYDevuelveSoloElTexto()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.OK, RespuestaDeClaude);

        var texto = await Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None);

        Assert.That(texto, Is.EqualTo("Hola"));
        Assert.That(manejador.UltimaSolicitud!.RequestUri!.Host, Is.EqualTo("api.anthropic.com"));
        Assert.That(manejador.UltimaSolicitud.Headers.Authorization!.ToString(), Does.Contain("clave-de-prueba"));
        using var cuerpo = JsonDocument.Parse(manejador.UltimoCuerpo);
        Assert.That(cuerpo.RootElement.GetProperty("model").GetString(), Is.EqualTo("claude-haiku-5-5"));
        Assert.That(cuerpo.RootElement.GetProperty("system").GetString(), Is.EqualTo("sistema"));
        Assert.That(cuerpo.RootElement.GetProperty("output_config").GetProperty("effort").GetString(), Is.EqualTo("low"));
        Assert.That(cuerpo.RootElement.GetProperty("thinking").GetProperty("type").GetString(), Is.EqualTo("disabled"));
        Assert.That(cuerpo.RootElement.TryGetProperty("temperature", out _), Is.False);
    }

    [Test]
    public async Task SinClaveNoLlamaAlProveedor()
    {
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);
        var manejador = new ManejadorFalso(HttpStatusCode.OK, RespuestaDeClaude);

        await Assert.ThrowsAsync<GeneradorDeTextoNoDisponibleException>(() =>
            Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None));
        Assert.That(manejador.Llamadas, Is.Zero);
    }

    [Test]
    public async Task UnaClaveInvalidaSeInformaConSuCodigo()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.Unauthorized, """{"type":"error","error":{"type":"authentication_error","message":"x"}}""");

        var excepcion = await Assert.ThrowsAsync<GeneradorDeTextoNoDisponibleException>(() =>
            Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None));

        Assert.That(excepcion!.CodigoHttp, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task UnRechazoDelModeloCuentaComoProveedorNoDisponible()
    {
        var manejador = new ManejadorFalso(HttpStatusCode.OK, RespuestaRechazada);

        await Assert.ThrowsAsync<GeneradorDeTextoNoDisponibleException>(() =>
            Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None));
    }
}
