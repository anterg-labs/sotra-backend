namespace Api.DTOs;

public class PaginaDto<T>
{
    public const int TamanoPaginaPorDefecto = 20;
    public const int TamanoPaginaMaximo = 100;

    public List<T> Elementos { get; init; } = new();
    public int Pagina { get; init; }
    public int TamanoPagina { get; init; }
    public int TotalElementos { get; init; }
    public int TotalPaginas { get; init; }

    public static bool EsValida(int pagina, int tamanoPagina)
    {
        if (pagina < 1)
        {
            return false;
        }

        if (tamanoPagina < 1 || tamanoPagina > TamanoPaginaMaximo)
        {
            return false;
        }

        return true;
    }

    public static PaginaDto<T> Crear(List<T> todos, int pagina, int tamanoPagina)
    {
        var totalPaginas = (todos.Count + tamanoPagina - 1) / tamanoPagina;
        var elementosDeLaPagina = todos
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .ToList();

        return new PaginaDto<T>
        {
            Elementos = elementosDeLaPagina,
            Pagina = pagina,
            TamanoPagina = tamanoPagina,
            TotalElementos = todos.Count,
            TotalPaginas = totalPaginas
        };
    }
}
