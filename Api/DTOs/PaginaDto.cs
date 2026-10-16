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

    public static object ErrorDePaginacion()
    {
        return new
        {
            codigo = "PAGINACION_INVALIDA",
            mensaje = "La página debe ser 1 o mayor y el tamaño de página debe estar entre 1 y "
                      + TamanoPaginaMaximo + "."
        };
    }

    public static bool Contiene(string? texto, string textoBuscado)
    {
        if (texto is null)
        {
            return false;
        }

        return texto.ToLower().Contains(textoBuscado);
    }

    public static PaginaDto<T> Crear(List<T> elementos, int totalElementos, int pagina, int tamanoPagina)
    {
        return new PaginaDto<T>
        {
            Elementos = elementos,
            Pagina = pagina,
            TamanoPagina = tamanoPagina,
            TotalElementos = totalElementos,
            TotalPaginas = (totalElementos + tamanoPagina - 1) / tamanoPagina
        };
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
