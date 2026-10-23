using Api.Infraestructura;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Api.PruebasUnitarias;

public class CacheDeAnaliticaPruebas
{
    private static CacheDeAnalitica Crear(int segundos = 30) =>
        new(new MemoryCache(new MemoryCacheOptions()), Options.Create(new OpcionesDeCacheDeAnalitica { Segundos = segundos }));

    [Test]
    public async Task LaSegundaLlamadaConLosMismosFiltrosNoCalculaDeNuevo()
    {
        var cache = Crear();
        var calculos = 0;
        Task<int> Calcular(CancellationToken _) => Task.FromResult(++calculos);

        var primera = await cache.ObtenerAsync("a", Calcular, CancellationToken.None);
        var segunda = await cache.ObtenerAsync("a", Calcular, CancellationToken.None);

        Assert.That(primera, Is.EqualTo(segunda));
        Assert.That(calculos, Is.EqualTo(1));
    }

    [Test]
    public async Task FiltrosDistintosNoComparten()
    {
        var cache = Crear();
        var calculos = 0;
        Task<int> Calcular(CancellationToken _) => Task.FromResult(++calculos);

        await cache.ObtenerAsync("a", Calcular, CancellationToken.None);
        await cache.ObtenerAsync("b", Calcular, CancellationToken.None);

        Assert.That(calculos, Is.EqualTo(2));
    }

    [Test]
    public async Task ConCeroSegundosLaCacheQuedaApagada()
    {
        var cache = Crear(0);
        var calculos = 0;
        Task<int> Calcular(CancellationToken _) => Task.FromResult(++calculos);

        await cache.ObtenerAsync("a", Calcular, CancellationToken.None);
        await cache.ObtenerAsync("a", Calcular, CancellationToken.None);

        Assert.That(calculos, Is.EqualTo(2));
    }

    [Test]
    public async Task SolicitudesSimultaneasComparteUnSoloCalculo()
    {
        var cache = Crear();
        var calculos = 0;
        var liberar = new TaskCompletionSource();

        async Task<int> Calcular(CancellationToken _)
        {
            Interlocked.Increment(ref calculos);
            await liberar.Task;
            return 7;
        }

        var tareas = Enumerable.Range(0, 5).Select(_ => cache.ObtenerAsync("a", Calcular, CancellationToken.None)).ToArray();
        liberar.SetResult();
        var resultados = await Task.WhenAll(tareas);

        Assert.That(calculos, Is.EqualTo(1));
        Assert.That(resultados, Is.All.EqualTo(7));
    }

    [Test]
    public async Task UnErrorNoQuedaGuardadoYLaSiguienteLlamadaReintenta()
    {
        var cache = Crear();
        var llamadas = 0;
        Task<int> Calcular(CancellationToken _)
        {
            llamadas++;
            return llamadas == 1 ? Task.FromException<int>(new InvalidOperationException("falla")) : Task.FromResult(5);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.ObtenerAsync("a", Calcular, CancellationToken.None));
        var valor = await cache.ObtenerAsync("a", Calcular, CancellationToken.None);

        Assert.That(valor, Is.EqualTo(5));
    }

    [Test]
    public async Task CancelarUnaSolicitudNoRompeElCalculoCompartido()
    {
        var cache = Crear();
        var liberar = new TaskCompletionSource();
        async Task<int> Calcular(CancellationToken _)
        {
            await liberar.Task;
            return 9;
        }

        using var cancelacion = new CancellationTokenSource();
        var cancelada = cache.ObtenerAsync("a", Calcular, cancelacion.Token);
        var otra = cache.ObtenerAsync("a", Calcular, CancellationToken.None);
        cancelacion.Cancel();
        liberar.SetResult();

        await Assert.CatchAsync<OperationCanceledException>(() => cancelada);
        Assert.That(await otra, Is.EqualTo(9));
        Assert.That(await cache.ObtenerAsync("a", Calcular, CancellationToken.None), Is.EqualTo(9));
    }
}
