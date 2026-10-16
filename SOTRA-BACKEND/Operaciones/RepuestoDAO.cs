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

    public (List<Repuesto> Elementos, int Total) ListarPagina(FiltroRepuestos filtro, int pagina, int tamanoPagina)
    {
        var consulta = _contexto.Repuestos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = ConsultasDeTexto.PatronContiene(filtro.Buscar);
            consulta = consulta.Where(r => EF.Functions.ILike(r.Codigo, patron)
                                           || EF.Functions.ILike(r.Nombre, patron)
                                           || (r.Marca != null && EF.Functions.ILike(r.Marca, patron))
                                           || (r.Descripcion != null && EF.Functions.ILike(r.Descripcion, patron)));
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(r => r.Estado == estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Ubicacion))
        {
            var patronUbicacion = ConsultasDeTexto.PatronContiene(filtro.Ubicacion);
            consulta = consulta.Where(r => r.Ubicacion != null && EF.Functions.ILike(r.Ubicacion, patronUbicacion));
        }

        if (filtro.BajoStock == true)
        {
            consulta = consulta.Where(r => r.Cantidad <= (r.CantMin ?? 0));
        }

        return ConsultasDeTexto.Paginar(consulta.OrderBy(r => r.IdRepuesto), pagina, tamanoPagina);
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
