using System.Collections.Concurrent;
using Api.Servicios;

namespace Api.Pruebas;

/// <summary>Reemplaza a R2 en las pruebas: guarda los archivos en memoria y puede simular que R2 no responde.</summary>
public sealed class AlmacenEnMemoria : IAlmacenDeArchivos
{
    public ConcurrentDictionary<string, byte[]> Archivos { get; } = new();

    public bool Falla { get; set; }

    public Task GuardarAsync(string clave, byte[] contenido, string tipoContenido, CancellationToken cancelacion)
    {
        if (Falla)
        {
            throw new AlmacenNoDisponibleException("R2 de prueba caído.");
        }

        Archivos[clave] = contenido;
        return Task.CompletedTask;
    }

    public Task<byte[]?> LeerAsync(string clave, CancellationToken cancelacion)
    {
        if (Falla)
        {
            throw new AlmacenNoDisponibleException("R2 de prueba caído.");
        }

        return Task.FromResult(Archivos.TryGetValue(clave, out var contenido) ? contenido : null);
    }

    public Task BorrarAsync(string clave, CancellationToken cancelacion)
    {
        Archivos.TryRemove(clave, out _);
        return Task.CompletedTask;
    }
}
