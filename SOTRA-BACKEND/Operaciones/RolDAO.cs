using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class RolDAO
{
    private readonly SotraContext contexto;

    public RolDAO() : this(new SotraContext())
    {
    }

    public RolDAO(SotraContext contexto)
    {
        this.contexto = contexto;
    }

    public Task<List<T>> ListarTodoAsync<T>(
        Expression<Func<Rol, T>> proyeccion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proyeccion);
        return contexto.Rols
            .AsNoTracking()
            .OrderBy(rol => rol.IdRol)
            .Select(proyeccion)
            .ToListAsync(cancellationToken);
    }
}
