using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;

namespace SOTRA_BACKEND.Operaciones;

/// <summary>
/// Lista cerrada de columnas por las que se puede ordenar un listado. El texto que llega de la API nunca se usa para
/// armar la consulta: solo se busca en esta lista, y lo que no está se rechaza antes de llegar a la base.
/// </summary>
public sealed class ColumnasOrdenables<T>
{
    private const string IntercalacionTexto = "es-UY-x-icu";
    private static readonly Expression<Func<string, string>> TransformarTexto =
        texto => EF.Functions.Collate(texto.ToLower(), IntercalacionTexto);

    private readonly Dictionary<string, Expression<Func<T, object?>>> _columnas;
    private readonly Expression<Func<T, object?>> _desempate;

    public ColumnasOrdenables(Expression<Func<T, object?>> desempate,
        IEnumerable<KeyValuePair<string, Expression<Func<T, object?>>>> columnas)
    {
        _desempate = desempate;
        _columnas = new Dictionary<string, Expression<Func<T, object?>>>(columnas, StringComparer.Ordinal);
    }

    public IReadOnlyCollection<string> Claves => _columnas.Keys;

    public bool Contiene(string? clave) => string.IsNullOrEmpty(clave) || _columnas.ContainsKey(clave);

    /// <summary>
    /// Ordena por la columna pedida y desempata siempre por el identificador, para que la paginación sea estable.
    /// Sin columna se ordena solo por el identificador, como antes.
    /// </summary>
    public IOrderedQueryable<T> Aplicar(IQueryable<T> consulta, string? clave, bool descendente)
    {
        if (string.IsNullOrEmpty(clave) || !_columnas.TryGetValue(clave, out var columna))
        {
            return descendente ? consulta.OrderByDescending(_desempate) : consulta.OrderBy(_desempate);
        }

        var cuerpo = columna.Body is UnaryExpression { NodeType: ExpressionType.Convert } conversion
            ? conversion.Operand
            : columna.Body;
        if (cuerpo.Type == typeof(string))
        {
            var nulo = Expression.Lambda<Func<T, bool>>(
                Expression.Equal(cuerpo, Expression.Constant(null, typeof(string))), columna.Parameters);
            var texto = Expression.Lambda<Func<T, string>>(
                new ReemplazarParametro(TransformarTexto.Parameters[0], cuerpo).Visit(TransformarTexto.Body)!,
                columna.Parameters);
            var primero = consulta.OrderBy(nulo);
            var alfabetico = descendente ? primero.ThenByDescending(texto) : primero.ThenBy(texto);
            return descendente ? alfabetico.ThenByDescending(_desempate) : alfabetico.ThenBy(_desempate);
        }

        var ordenada = descendente ? consulta.OrderByDescending(columna) : consulta.OrderBy(columna);
        return descendente ? ordenada.ThenByDescending(_desempate) : ordenada.ThenBy(_desempate);
    }

    private sealed class ReemplazarParametro(ParameterExpression origen, Expression destino) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression parametro)
            => parametro == origen ? destino : base.VisitParameter(parametro);
    }
}
