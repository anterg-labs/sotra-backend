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
