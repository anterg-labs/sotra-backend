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

    public List<Rol> ListarTodo()
    {
        return _contexto.Rols
            .OrderBy(rol => rol.IdRol)
            .ToList();
    }
}
