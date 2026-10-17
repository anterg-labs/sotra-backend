using Microsoft.EntityFrameworkCore;

namespace SOTRA_BACKEND.Operaciones;

public static class ConsultasDeTexto
{
    /// <summary>Arma un patrón ILIKE de "contiene", escapando los comodines que escriba la persona.</summary>
    public static string PatronContiene(string texto)
    {
        var escapado = texto.Trim()
            .Replace(@"\", @"\\")
            .Replace("%", @"\%")
            .Replace("_", @"\_");
        return $"%{escapado}%";
    }

    /// <summary>Traduce "activo" o "inactivo" a booleano. Devuelve falso si el texto no es un estado conocido.</summary>
    public static bool TryTraducirEstado(string? texto, out bool? estado)
    {
        estado = null;
        if (string.IsNullOrEmpty(texto)) return true;
        if (texto == "activo") estado = true;
        else if (texto == "inactivo") estado = false;
        else return false;
        return true;
    }

    public static (List<T> Elementos, int Total) Paginar<T>(IQueryable<T> consulta, int pagina, int tamanoPagina)
    {
        var total = consulta.Count();
        var elementos = consulta.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToList();
        return (elementos, total);
    }

    public static async Task<(List<T> Elementos, int Total)> PaginarAsync<T>(
        IQueryable<T> consulta, int pagina, int tamanoPagina, CancellationToken cancelacion)
    {
        var total = await consulta.CountAsync(cancelacion);
        var elementos = await consulta.Skip((pagina - 1) * tamanoPagina).Take(tamanoPagina).ToListAsync(cancelacion);
        return (elementos, total);
    }
}
