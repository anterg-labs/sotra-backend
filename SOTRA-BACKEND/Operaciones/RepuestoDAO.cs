using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class RepuestoDAO
{
    private readonly SotraContext _contexto;

    public RepuestoDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }


    public List<Repuesto> ListarTodo()
    {
        return _contexto.Repuestos
            .OrderBy(repuesto => repuesto.IdRepuesto)
            .ToList();
    }

    public Repuesto? Seleccionar(long id)
    {
        return _contexto.Repuestos.FirstOrDefault(repuesto => repuesto.IdRepuesto == id);
    }

    public Repuesto Insertar(Repuesto repuesto)
    {
        _contexto.Repuestos.Add(repuesto);
        _contexto.SaveChanges();
        return repuesto;
    }

    public void Actualizar(Repuesto repuesto)
    {
        _contexto.SaveChanges();
    }

    public bool Desactivar(long id)
    {
        var repuesto = Seleccionar(id);
        if (repuesto is null)
        {
            return false;
        }

        repuesto.Estado = false;
        _contexto.SaveChanges();
        return true;
    }
}
