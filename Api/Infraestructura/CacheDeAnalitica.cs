using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Api.Infraestructura;

/// <summary>
/// Reutiliza unos segundos el resultado de una consulta del dashboard para los mismos filtros. Las solicitudes
/// simultáneas con la misma clave comparten un único cálculo, que no depende de la cancelación de quien lo inició.
/// </summary>
public sealed class CacheDeAnalitica(IMemoryCache cache, IOptions<OpcionesDeCacheDeAnalitica> opciones)
{
    private readonly TimeSpan _duracion = TimeSpan.FromSeconds(opciones.Value.Segundos);

    public async Task<T> ObtenerAsync<T>(string clave, Func<CancellationToken, Task<T>> calcular, CancellationToken cancelacion)
    {
        if (_duracion <= TimeSpan.Zero)
        {
            return await calcular(cancelacion);
        }

        var claveCompleta = $"analitica|{typeof(T).Name}|{clave}";
        var calculo = cache.GetOrCreate(claveCompleta, entrada =>
        {
            entrada.AbsoluteExpirationRelativeToNow = _duracion;
            return new Lazy<Task<T>>(() => calcular(CancellationToken.None));
        })!;

        try
        {
            return await calculo.Value.WaitAsync(cancelacion);
        }
        catch (Exception) when (calculo.Value.IsFaulted)
        {
            cache.Remove(claveCompleta);
            throw;
        }
    }
}
