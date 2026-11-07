using Api.Servicios;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.PruebasUnitarias;

public class TraductorDeConsultasPruebas
{
    private sealed class GeneradorFalso(Func<CancellationToken, Task<string>> generar) : IGeneradorDeTexto
    {
        public int Llamadas { get; private set; }

        public Task<string> GenerarAsync(string instrucciones, string mensaje, CancellationToken cancellationToken)
        {
            Llamadas++;
            return generar(cancellationToken);
        }
    }

    private static TraductorDeConsultas Crear(IGeneradorDeTexto generador) =>
        new(generador, new MemoryCache(new MemoryCacheOptions()), NullLogger<TraductorDeConsultas>.Instance);

    [Test]
    public async Task DevuelveLaTraduccionEnUnaSolaLineaYSinComillas()
    {
        var generador = new GeneradorFalso(_ => Task.FromResult("\"engine oil change\"\nNota: traducción literal"));

        var traduccion = await Crear(generador).TraducirAsync("cambio de aceite", CancellationToken.None);

        Assert.That(traduccion, Is.EqualTo("engine oil change"));
    }

    [Test]
    public async Task LaMismaConsultaNoGastaOtraLlamadaAlProveedor()
    {
        var generador = new GeneradorFalso(_ => Task.FromResult("engine oil change"));
        var traductor = Crear(generador);

        await traductor.TraducirAsync("Cambio de aceite", CancellationToken.None);
        var repetida = await traductor.TraducirAsync("cambio de aceite ", CancellationToken.None);

        Assert.That(repetida, Is.EqualTo("engine oil change"));
        Assert.That(generador.Llamadas, Is.EqualTo(1));
    }

    [Test]
    public async Task UnaConsultaYaEnInglesNoVuelveAPreguntarse()
    {
        var generador = new GeneradorFalso(_ => Task.FromResult("Change the engine oil"));
        var traductor = Crear(generador);

        Assert.That(await traductor.TraducirAsync("Change the engine oil", CancellationToken.None), Is.Null);
        Assert.That(await traductor.TraducirAsync("Change the engine oil", CancellationToken.None), Is.Null);
        Assert.That(generador.Llamadas, Is.EqualTo(1));
    }

    [Test]
    public async Task SiElProveedorFallaBuscaSoloConLaOriginalYReintentaLaProximaVez()
    {
        var generador = new GeneradorFalso(_ => throw new GeneradorDeTextoNoDisponibleException("falla de prueba"));
        var traductor = Crear(generador);

        Assert.That(await traductor.TraducirAsync("cambio de aceite", CancellationToken.None), Is.Null);
        Assert.That(await traductor.TraducirAsync("cambio de aceite", CancellationToken.None), Is.Null);
        Assert.That(generador.Llamadas, Is.EqualTo(2));
    }

    [Test]
    public async Task SiElProveedorTardaBuscaSoloConLaOriginal()
    {
        var generador = new GeneradorFalso(_ => throw new TaskCanceledException("tiempo agotado"));

        Assert.That(await Crear(generador).TraducirAsync("cambio de aceite", CancellationToken.None), Is.Null);
    }

    [Test]
    public void SiElUsuarioCancelaNoSeOcultaLaCancelacion()
    {
        using var cancelacion = new CancellationTokenSource();
        cancelacion.Cancel();
        var generador = new GeneradorFalso(token => Task.FromCanceled<string>(token));

        Assert.CatchAsync<OperationCanceledException>(() =>
            Crear(generador).TraducirAsync("cambio de aceite", cancelacion.Token));
    }

    [Test]
    public async Task UnaConsultaMuyLargaNoSeTraduce()
    {
        var generador = new GeneradorFalso(_ => Task.FromResult("traducción"));

        var traduccion = await Crear(generador).TraducirAsync(new string('a', 301), CancellationToken.None);

        Assert.That(traduccion, Is.Null);
        Assert.That(generador.Llamadas, Is.Zero);
    }

    [TestCase("Mantenimiento de 500 horas", "500 hours of service maintenance", "500 hours of service maintenance", TestName = "ConservaElNumero")]
    [TestCase("Mantenimiento de 1000 horas", "1,000 hours of service", "1,000 hours of service", TestName = "AceptaLosMilesConSeparador")]
    [TestCase("Mantenimiento de 500 horas", "50 hours of service", null, TestName = "DescartaUnNumeroCambiado")]
    [TestCase("Mantenimiento de 500 horas", "Maintenance", null, TestName = "DescartaUnNumeroPerdido")]
    [TestCase("Cambio de aceite", "Oil change every 500 hours", null, TestName = "DescartaUnNumeroAgregado")]
    [TestCase("Cambio de aceite", "   ", null, TestName = "DescartaUnaRespuestaVacia")]
    [TestCase("Change engine oil", "change ENGINE oil", null, TestName = "DescartaLaMismaConsulta")]
    public void ValidaLaTraduccion(string consulta, string generado, string? esperado)
    {
        Assert.That(TraductorDeConsultas.ValidarTraduccion(consulta, generado), Is.EqualTo(esperado));
    }

    [TestCase("Park the machine on a level surface and remove the filter from the housing.", true, TestName = "ReconoceUnTextoEnIngles")]
    [TestCase("Estacione la máquina sobre una superficie plana y retire el filtro de la carcasa.", false, TestName = "ReconoceUnTextoEnEspanol")]
    [TestCase("John Deere 3000", false, TestName = "SinPalabrasFrecuentesNoLoDaPorIngles")]
    public void ReconoceElIdiomaDelTexto(string texto, bool esperado)
    {
        Assert.That(TraductorDeConsultas.EstaEnIngles(texto), Is.EqualTo(esperado));
    }

    [Test]
    public void LaBusquedaPorPalabrasIgnoraLasPalabrasSinValorEnIngles()
    {
        Assert.That(ServicioSugerencias.CrearBusquedaTextual("Disassemble the main pump for the engine"),
            Is.EqualTo("disassemble | main | pump | engine"));
    }

    [Test]
    public void LasInstruccionesPidenSumarLaInspeccionCuandoLaConsultaDescribeUnaFalla()
    {
        Assert.That(TraductorDeConsultas.Instrucciones, Does.Contain("describe una falla"));
        Assert.That(TraductorDeConsultas.Instrucciones, Does.Contain("inspect and check the air conditioning system"));
        Assert.That(TraductorDeConsultas.Instrucciones, Does.Contain("sin suponer causas"));
        Assert.That(TraductorDeConsultas.Instrucciones, Does.Contain("oil filter replacement"));
    }

    [Test]
    public void DescartaUnaRespuestaDemasiadoLarga()
    {
        Assert.That(TraductorDeConsultas.ValidarTraduccion("Cambio de aceite", new string('a', 301)), Is.Null);
    }
}
