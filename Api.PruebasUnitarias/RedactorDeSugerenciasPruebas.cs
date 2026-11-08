using System.Net;
using Api.Servicios;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.PruebasUnitarias;

public class RedactorDeSugerenciasPruebas
{
    private const string Consulta = "cambio de aceite del motor";
    private const string TextoDelManual =
        "El aceite del motor se cambia cada 250 horas de trabajo. Utilice aceite 15W-40. " +
        "La capacidad del carter es de 14 litros con filtro.";

    private static readonly SeccionDocumento Seccion = new(1, 1, "manual.pdf", "4.3 Cambio de aceite del motor", "",
        TextoDelManual, false);

    private sealed class GeneradorFalso(Func<CancellationToken, Task<string>> generar) : IGeneradorDeTexto
    {
        public string? UltimasInstrucciones { get; private set; }

        public Task<string> GenerarAsync(string instrucciones, string mensaje, CancellationToken cancellationToken)
        {
            UltimasInstrucciones = instrucciones;
            return generar(cancellationToken);
        }
    }

    private static Task<SugerenciaRedactada> ResumirAsync(IGeneradorDeTexto generador,
        CancellationToken cancellationToken = default) =>
        new RedactorDeSugerencias(generador, NullLogger<RedactorDeSugerencias>.Instance)
            .ResumirAsync(Consulta, Seccion, TextoDelManual, cancellationToken);

    private sealed class GeneradorQueGuardaElMensaje(string respuesta) : IGeneradorDeTexto
    {
        public string Mensaje { get; private set; } = "";

        public Task<string> GenerarAsync(string instrucciones, string mensaje, CancellationToken cancellationToken)
        {
            Mensaje = mensaje;
            return Task.FromResult(respuesta);
        }
    }

    private static readonly List<string> AdvertenciasEnIngles =
    [
        "Do not perform this task when the engine is hot.", "Dispose of the contaminated oil properly.",
    ];

    private static Task<SugerenciaRedactada> ResumirConAdvertenciasAsync(IGeneradorDeTexto generador, List<string> advertencias) =>
        new RedactorDeSugerencias(generador, NullLogger<RedactorDeSugerencias>.Instance)
            .ResumirAsync(Consulta, Seccion, TextoDelManual, CancellationToken.None, advertencias);

    [Test]
    public async Task LasAdvertenciasEnInglesSePidenYSeDevuelvenTraducidas()
    {
        var generador = new GeneradorQueGuardaElMensaje(
            "Título: Cambio de aceite del motor\nCada 250 horas de trabajo se cambia el aceite con aceite 15W-40.\n" +
            "Advertencia: No realice esta tarea con el motor caliente.\nAdvertencia: Deseche el aceite contaminado correctamente.");

        var redactada = await ResumirConAdvertenciasAsync(generador, AdvertenciasEnIngles);

        Assert.That(generador.Mensaje, Does.Contain("Do not perform this task when the engine is hot."));
        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.Ia));
        Assert.That(redactada.Texto, Is.EqualTo("Cada 250 horas de trabajo se cambia el aceite con aceite 15W-40."));
        Assert.That(redactada.Advertencias, Is.EqualTo(new[]
        {
            "No realice esta tarea con el motor caliente.", "Deseche el aceite contaminado correctamente.",
        }));
    }

    [Test]
    public async Task SiFaltaUnaAdvertenciaTraducidaSeMuestranLasOriginales()
    {
        var generador = new GeneradorQueGuardaElMensaje(
            "Cada 250 horas de trabajo se cambia el aceite con aceite 15W-40.\nAdvertencia: No realice esta tarea con el motor caliente.");

        var redactada = await ResumirConAdvertenciasAsync(generador, AdvertenciasEnIngles);

        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.Ia));
        Assert.That(redactada.Texto, Does.Not.Contain("Advertencia"));
        Assert.That(redactada.Advertencias, Is.Null);
    }

    [Test]
    public async Task UnaAdvertenciaTraducidaConUnNumeroInventadoDescartaElResumen()
    {
        var generador = new GeneradorQueGuardaElMensaje(
            "Cada 250 horas de trabajo se cambia el aceite con aceite 15W-40.\n" +
            "Advertencia: Espere 30 minutos a que el motor se enfríe.\nAdvertencia: Deseche el aceite contaminado correctamente.");

        var redactada = await ResumirConAdvertenciasAsync(generador, AdvertenciasEnIngles);

        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.Descartada));
    }

    [Test]
    public async Task LasAdvertenciasEnEspanolNoSePidenAlModelo()
    {
        var generador = new GeneradorQueGuardaElMensaje("Cada 250 horas de trabajo se cambia el aceite con aceite 15W-40.");

        var redactada = await ResumirConAdvertenciasAsync(generador, ["No realice esta tarea con el motor caliente."]);

        Assert.That(generador.Mensaje, Does.Not.Contain("Advertencias del manual"));
        Assert.That(redactada.Advertencias, Is.Null);
    }

    [Test]
    public async Task UnResumenConfiableSeDevuelveEnModoIa()
    {
        var resumen = "Cada 250 horas de trabajo se debe cambiar el aceite del motor con aceite 15W-40.";
        var generador = new GeneradorFalso(_ => Task.FromResult(resumen));

        var redactada = await ResumirAsync(generador);

        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.Ia));
        Assert.That(redactada.Texto, Is.EqualTo(resumen));
        Assert.That(generador.UltimasInstrucciones, Is.EqualTo(RedaccionDeSugerencia.Instrucciones));
    }

    [Test]
    public async Task ElTituloDeLaIaSeDevuelveAparteDelResumen()
    {
        var generador = new GeneradorFalso(_ => Task.FromResult(
            "Título: Cambio de aceite del motor\nCada 250 horas de trabajo se debe cambiar el aceite con aceite 15W-40."));

        var redactada = await ResumirAsync(generador);

        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.Ia));
        Assert.That(redactada.Titulo, Is.EqualTo("Cambio de aceite del motor"));
        Assert.That(redactada.Texto, Is.EqualTo("Cada 250 horas de trabajo se debe cambiar el aceite con aceite 15W-40."));
    }

    [TestCase("SIN_INFORMACION", TestName = "SiLaSeccionNoTrataLaTareaNoHaySugerencia")]
    [TestCase("Título: Precauciones al manipular componentes hidráulicos\nEste texto del manual no contiene información sobre la inspección de mangueras. Por esa razón no se puede elaborar un resumen de la tarea consultada.",
        TestName = "SiElModeloExplicaElRechazoEnProsaTambienCuentaComoSinInformacion")]
    [TestCase("Título: Cambio de aceite del motor\nEl texto no contiene instrucciones específicas para cambiar el aceite del motor.",
        TestName = "SiElModeloDiceQueElTextoNoTieneLaTareaCuentaComoSinInformacion")]
    [TestCase("**sin_informacion**.", TestName = "SiLaSeccionNoTrataLaTareaNoHaySugerenciaAunqueVengaConFormato")]
    public async Task SiLaSeccionNoTrataLaTareaNoHaySugerencia(string respuestaDelModelo)
    {
        var generador = new GeneradorFalso(_ => Task.FromResult(respuestaDelModelo));

        var redactada = await ResumirAsync(generador);

        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.SinInformacion));
        Assert.That(redactada.Texto, Is.Empty);
    }

    [Test]
    public async Task UnTituloConUnNumeroInventadoDescartaLaRespuesta()
    {
        var generador = new GeneradorFalso(_ => Task.FromResult(
            "Título: Servicio de las 500 horas\nCada 250 horas de trabajo se debe cambiar el aceite del motor."));

        var redactada = await ResumirAsync(generador);

        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.Descartada));
        Assert.That(redactada.Titulo, Is.Empty);
    }

    [Test]
    public async Task UnResumenConUnNumeroInventadoSeDescartaSinAviso()
    {
        var generador = new GeneradorFalso(_ => Task.FromResult("Cambie el aceite cada 500 horas con 20 litros."));

        var redactada = await ResumirAsync(generador);

        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.Descartada));
        Assert.That(redactada.Texto, Is.EqualTo(Api.Servicios.TextoDelManual.CrearResumenExtractivo(TextoDelManual)));
    }

    [TestCase(null, TestName = "SinClaveConfiguradaUsaElTextoDelManualConAviso")]
    [TestCase(HttpStatusCode.Unauthorized, TestName = "ConClaveInvalidaUsaElTextoDelManualConAviso")]
    [TestCase(HttpStatusCode.ServiceUnavailable, TestName = "ConElProveedorCaidoUsaElTextoDelManualConAviso")]
    [TestCase(HttpStatusCode.TooManyRequests, TestName = "ConElLimiteDeLlamadasAlcanzadoUsaElTextoDelManualConAviso")]
    public async Task SiElProveedorFallaUsaElTextoDelManualConAviso(HttpStatusCode? codigo)
    {
        var generador = new GeneradorFalso(_ =>
            throw new GeneradorDeTextoNoDisponibleException("falla de prueba", codigo));

        var redactada = await ResumirAsync(generador);

        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.SinIa));
        Assert.That(redactada.Texto, Is.EqualTo(Api.Servicios.TextoDelManual.CrearResumenExtractivo(TextoDelManual)));
    }

    [Test]
    public async Task SiElProveedorNoRespondeATiempoUsaElTextoDelManualConAviso()
    {
        var generador = new GeneradorFalso(_ => throw new TaskCanceledException("tiempo agotado"));

        var redactada = await ResumirAsync(generador);

        Assert.That(redactada.Modo, Is.EqualTo(ModosDeSugerencia.SinIa));
    }

    [Test]
    public async Task SiElUsuarioCancelaNoSeOcultaLaCancelacion()
    {
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        var generador = new GeneradorFalso(token => Task.FromCanceled<string>(token));

        await Assert.CatchAsync<OperationCanceledException>(() => ResumirAsync(generador, cancelacion.Token));
    }
}
