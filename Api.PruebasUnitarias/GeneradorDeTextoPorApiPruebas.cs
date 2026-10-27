using System.Net;
using System.Text.Json;
using Api.Infraestructura;
using Api.Servicios;
using Microsoft.Extensions.DependencyInjection;

namespace Api.PruebasUnitarias;

public class GeneradorDeTextoPorApiPruebas
{
    private const string RespuestaDeChat = """{"choices":[{"message":{"role":"assistant","content":" Hola "}}]}""";

    private static readonly string[] Variables = ["IA_API_URL", "IA_API_MODELO", "IA_API_CLAVE", "IA_API_ESFUERZO", "IA_API_LLAMADAS_POR_MINUTO"];

    private readonly Dictionary<string, string?> _valoresAnteriores = new();

    [SetUp]
    public void Preparar()
    {
        foreach (var variable in Variables)
        {
            _valoresAnteriores[variable] = Environment.GetEnvironmentVariable(variable);
            Environment.SetEnvironmentVariable(variable, null);
        }

        Environment.SetEnvironmentVariable("IA_API_CLAVE", "clave-de-prueba");
    }

    [TearDown]
    public void Limpiar()
    {
        foreach (var variable in Variables)
        {
            Environment.SetEnvironmentVariable(variable, _valoresAnteriores[variable]);
        }
    }

    private sealed class ManejadorFalso(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Llamadas { get; private set; }
        public HttpRequestMessage? UltimaSolicitud { get; private set; }
        public string UltimoCuerpo { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Llamadas++;
            UltimaSolicitud = solicitud;
            UltimoCuerpo = solicitud.Content is null ? "" : await solicitud.Content.ReadAsStringAsync(cancellationToken);
            return responder(Llamadas);
        }
    }

    private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo = "{}") =>
        new(codigo) { Content = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json") };

    private static IGeneradorDeTexto Crear(ManejadorFalso manejador)
    {
        var servicios = new ServiceCollection();
        servicios.AgregarGeneradorDeTexto(opciones => opciones.Retry.Delay = TimeSpan.Zero);
        servicios.AddHttpClient<IGeneradorDeTexto, GeneradorDeTextoPorApi>().ConfigurePrimaryHttpMessageHandler(() => manejador);
        return servicios.BuildServiceProvider().GetRequiredService<IGeneradorDeTexto>();
    }

    [Test]
    public async Task PorDefectoLlamaAGroqConElModeloYLaClave()
    {
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.OK, RespuestaDeChat));

        var texto = await Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None);

        Assert.That(texto, Is.EqualTo("Hola"));
        Assert.That(manejador.UltimaSolicitud!.RequestUri!.ToString(), Is.EqualTo("https://api.groq.com/openai/v1/chat/completions"));
        Assert.That(manejador.UltimaSolicitud.Headers.Authorization!.ToString(), Is.EqualTo("Bearer clave-de-prueba"));
        using var cuerpo = JsonDocument.Parse(manejador.UltimoCuerpo);
        Assert.That(cuerpo.RootElement.GetProperty("model").GetString(), Is.EqualTo("openai/gpt-oss-20b"));
        Assert.That(cuerpo.RootElement.GetProperty("messages")[0].GetProperty("content").GetString(), Is.EqualTo("sistema"));
        Assert.That(cuerpo.RootElement.GetProperty("messages")[1].GetProperty("content").GetString(), Is.EqualTo("mensaje"));
        Assert.That(cuerpo.RootElement.TryGetProperty("reasoning_effort", out _), Is.False);
    }

    [Test]
    public async Task UsaElProveedorYElEsfuerzoConfigurados()
    {
        Environment.SetEnvironmentVariable("IA_API_URL", "https://otro.proveedor.test/v1");
        Environment.SetEnvironmentVariable("IA_API_MODELO", "modelo-x");
        Environment.SetEnvironmentVariable("IA_API_ESFUERZO", "low");
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.OK, RespuestaDeChat));

        await Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None);

        Assert.That(manejador.UltimaSolicitud!.RequestUri!.ToString(), Is.EqualTo("https://otro.proveedor.test/v1/chat/completions"));
        using var cuerpo = JsonDocument.Parse(manejador.UltimoCuerpo);
        Assert.That(cuerpo.RootElement.GetProperty("model").GetString(), Is.EqualTo("modelo-x"));
        Assert.That(cuerpo.RootElement.GetProperty("reasoning_effort").GetString(), Is.EqualTo("low"));
    }

    [Test]
    public async Task SinClaveFallaSinLlamarAlProveedor()
    {
        Environment.SetEnvironmentVariable("IA_API_CLAVE", null);
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.OK, RespuestaDeChat));

        var excepcion = await Assert.ThrowsAsync<GeneradorDeTextoNoDisponibleException>(() =>
            Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None));

        Assert.That(excepcion!.CodigoHttp, Is.Null);
        Assert.That(manejador.Llamadas, Is.EqualTo(0));
    }

    [Test]
    public async Task ReintentaUnaVezUnFalloPasajero()
    {
        var manejador = new ManejadorFalso(n => n == 1 ? Respuesta(HttpStatusCode.ServiceUnavailable) : Respuesta(HttpStatusCode.OK, RespuestaDeChat));

        var texto = await Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None);

        Assert.That(texto, Is.EqualTo("Hola"));
        Assert.That(manejador.Llamadas, Is.EqualTo(2));
    }

    [Test]
    public async Task UnaClaveInvalidaNoSeReintentaEInformaElCodigo()
    {
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.Unauthorized, """{"error":"clave"}"""));

        var excepcion = await Assert.ThrowsAsync<GeneradorDeTextoNoDisponibleException>(() =>
            Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None));

        Assert.That(excepcion!.CodigoHttp, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(manejador.Llamadas, Is.EqualTo(1));
    }

    [Test]
    public async Task UnaRespuestaInesperadaEsUnFalloDelProveedor()
    {
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.OK, """{"sin":"choices"}"""));

        await Assert.ThrowsAsync<GeneradorDeTextoNoDisponibleException>(() =>
            Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None));
    }

    [Test]
    public async Task ConElProveedorCaidoCortaElCircuitoYFallaSinLlamarlo()
    {
        // Sin el límite de llamadas por minuto de por medio: acá se prueba solo el circuito.
        Environment.SetEnvironmentVariable("IA_API_LLAMADAS_POR_MINUTO", "100");
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.ServiceUnavailable));
        var generador = Crear(manejador);

        for (var intento = 0; intento < 10; intento++)
        {
            await VerificarQueFallaAsync(generador);
        }

        var llamadasConElCircuitoCerrado = manejador.Llamadas;
        await VerificarQueFallaAsync(generador);
        Assert.That(manejador.Llamadas, Is.EqualTo(llamadasConElCircuitoCerrado), "con el circuito abierto no debe llegar al proveedor");
    }

    [Test]
    public async Task PorDefectoPermiteDiezLlamadasPorMinutoYLaSiguienteNoLlegaAlProveedor()
    {
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.OK, RespuestaDeChat));
        var generador = Crear(manejador);

        for (var llamada = 0; llamada < 10; llamada++)
        {
            Assert.That(await generador.GenerarAsync("sistema", "mensaje", CancellationToken.None), Is.EqualTo("Hola"));
        }

        var excepcion = await Assert.ThrowsAsync<GeneradorDeTextoNoDisponibleException>(() =>
            generador.GenerarAsync("sistema", "mensaje", CancellationToken.None));

        Assert.That(excepcion!.CodigoHttp, Is.EqualTo(HttpStatusCode.TooManyRequests));
        Assert.That(manejador.Llamadas, Is.EqualTo(10));
    }

    [Test]
    public async Task ElLimiteEsUnoSoloParaTodosLosClientesDelProveedor()
    {
        Environment.SetEnvironmentVariable("IA_API_LLAMADAS_POR_MINUTO", "2");
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.OK, RespuestaDeChat));
        var servicios = new ServiceCollection();
        servicios.AgregarGeneradorDeTexto(opciones => opciones.Retry.Delay = TimeSpan.Zero);
        servicios.AddHttpClient<IGeneradorDeTexto, GeneradorDeTextoPorApi>().ConfigurePrimaryHttpMessageHandler(() => manejador);
        var proveedor = servicios.BuildServiceProvider();

        // Cada pedido HTTP recibe un generador nuevo; el límite tiene que contarlos juntos.
        await proveedor.GetRequiredService<IGeneradorDeTexto>().GenerarAsync("sistema", "mensaje", CancellationToken.None);
        await proveedor.GetRequiredService<IGeneradorDeTexto>().GenerarAsync("sistema", "mensaje", CancellationToken.None);
        await VerificarQueFallaAsync(proveedor.GetRequiredService<IGeneradorDeTexto>());

        Assert.That(manejador.Llamadas, Is.EqualTo(2));
    }

    [Test]
    public async Task ElReintentoDeUnFalloPasajeroNoCuentaComoOtraLlamada()
    {
        Environment.SetEnvironmentVariable("IA_API_LLAMADAS_POR_MINUTO", "1");
        var manejador = new ManejadorFalso(n => n == 1 ? Respuesta(HttpStatusCode.ServiceUnavailable) : Respuesta(HttpStatusCode.OK, RespuestaDeChat));

        var texto = await Crear(manejador).GenerarAsync("sistema", "mensaje", CancellationToken.None);

        Assert.That(texto, Is.EqualTo("Hola"));
        Assert.That(manejador.Llamadas, Is.EqualTo(2));
    }

    [TestCase("0")]
    [TestCase("-3")]
    [TestCase("diez")]
    public void UnLimiteDeLlamadasInvalidoNoArranca(string valor)
    {
        Environment.SetEnvironmentVariable("IA_API_LLAMADAS_POR_MINUTO", valor);

        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AgregarGeneradorDeTexto());
    }

    private static async Task VerificarQueFallaAsync(IGeneradorDeTexto generador)
    {
        try
        {
            await generador.GenerarAsync("sistema", "mensaje", CancellationToken.None);
            Assert.Fail("se esperaba que el proveedor fallara");
        }
        catch (GeneradorDeTextoNoDisponibleException)
        {
        }
    }

    [Test]
    public async Task RespetaLaCancelacionDelPedido()
    {
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.OK, RespuestaDeChat));
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(() => Crear(manejador).GenerarAsync("sistema", "mensaje", cancelacion.Token));
    }
}
