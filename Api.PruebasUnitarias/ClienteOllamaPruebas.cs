using System.Net;
using Api.Infraestructura;
using Api.Servicios;
using Microsoft.Extensions.DependencyInjection;

namespace Api.PruebasUnitarias;

public class ClienteOllamaPruebas
{
    private const string RespuestaDeChat = """{"message":{"content":" Hola "}}""";

    private string? _urlAnterior;

    [SetUp]
    public void Preparar()
    {
        _urlAnterior = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL");
        Environment.SetEnvironmentVariable("OLLAMA_BASE_URL", "http://ollama.prueba:11434");
    }

    [TearDown]
    public void Limpiar() => Environment.SetEnvironmentVariable("OLLAMA_BASE_URL", _urlAnterior);

    private sealed class ManejadorFalso(Func<int, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Llamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage solicitud, CancellationToken cancellationToken)
        {
            Llamadas++;
            return Task.FromResult(responder(Llamadas));
        }
    }

    private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo = "{}") =>
        new(codigo) { Content = new StringContent(cuerpo, System.Text.Encoding.UTF8, "application/json") };

    private static ServicioOllama Crear(ManejadorFalso manejador)
    {
        var servicios = new ServiceCollection();
        servicios.AgregarClienteOllama(opciones => opciones.Retry.Delay = TimeSpan.Zero);
        servicios.AddHttpClient<ServicioOllama>().ConfigurePrimaryHttpMessageHandler(() => manejador);
        return servicios.BuildServiceProvider().GetRequiredService<ServicioOllama>();
    }

    [Test]
    public async Task ReintentaCuandoOllamaDevuelve503YDespuesResponde()
    {
        var manejador = new ManejadorFalso(n => n < 3 ? Respuesta(HttpStatusCode.ServiceUnavailable) : Respuesta(HttpStatusCode.OK, RespuestaDeChat));

        var texto = await Crear(manejador).GenerarTextoAsync("sistema", "mensaje", CancellationToken.None);

        Assert.That(texto, Is.EqualTo("Hola"));
        Assert.That(manejador.Llamadas, Is.EqualTo(3));
    }

    [Test]
    public async Task NoReintentaUnErrorDelCliente()
    {
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.BadRequest));

        await Assert.ThrowsAsync<HttpRequestException>(() => Crear(manejador).GenerarTextoAsync("sistema", "mensaje", CancellationToken.None));
        Assert.That(manejador.Llamadas, Is.EqualTo(1));
    }

    [Test]
    public async Task ConOllamaCaidoCortaElCircuitoYFallaRapidoComoHttpRequestException()
    {
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.ServiceUnavailable));
        var servicio = Crear(manejador);

        for (var i = 0; i < 10; i++)
        {
            await Assert.ThrowsAsync<HttpRequestException>(() => servicio.GenerarTextoAsync("sistema", "mensaje", CancellationToken.None));
        }

        var llamadasConElCircuitoCerrado = manejador.Llamadas;
        await Assert.ThrowsAsync<HttpRequestException>(() => servicio.GenerarTextoAsync("sistema", "mensaje", CancellationToken.None));
        Assert.That(manejador.Llamadas, Is.EqualTo(llamadasConElCircuitoCerrado), "con el circuito abierto no debe llegar a Ollama");
    }

    [Test]
    public async Task RespetaLaCancelacion()
    {
        var manejador = new ManejadorFalso(_ => Respuesta(HttpStatusCode.OK, RespuestaDeChat));
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(() => Crear(manejador).GenerarTextoAsync("sistema", "mensaje", cancelacion.Token));
    }
}
