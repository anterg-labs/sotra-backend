using System.ComponentModel.DataAnnotations;

namespace Api.Infraestructura;

/// <summary>Caché corta de las consultas del dashboard analítico.</summary>
public sealed class OpcionesDeCacheDeAnalitica
{
    public const string Seccion = "CacheAnalitica";

    /// <summary>Segundos que se reutiliza un resultado para los mismos filtros. Con 0 la caché queda apagada.</summary>
    [Range(0, 600)]
    public int Segundos { get; init; } = 30;
}
