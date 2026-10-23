using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class RolDAO
{
    private readonly SotraContext _contexto;

    public RolDAO() : this(new SotraContext())
    {
    }

    public RolDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<List<Rol>> ListarTodoAsync(CancellationToken cancelacion)
    {
        return await _contexto.Rols
            .OrderBy(rol => rol.IdRol)
            .ToListAsync(cancelacion);
    }
}
