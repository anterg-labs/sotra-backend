using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class TipoMaquinaDAO
{
    private readonly SotraContext contexto;

    public TipoMaquinaDAO() : this(new SotraContext())
    {
    }

    public TipoMaquinaDAO(SotraContext contexto)
    {
        this.contexto = contexto;
    }

    public Task<List<T>> ListarTodoAsync<T>(
        Expression<Func<TipoMaquina, T>> proyeccion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proyeccion);
        return contexto.TipoMaquinas
            .AsNoTracking()
            .OrderBy(tipo => tipo.IdTipoMaquina)
            .Select(proyeccion)
            .ToListAsync(cancellationToken);
    }

    public List<TipoMaquina> listarTodo()
    {
        return contexto.TipoMaquinas
            .OrderBy(tipo => tipo.IdTipoMaquina)
            .ToList();
    }

    public TipoMaquina? seleccionarTipoMaquina(long id)
    {
        return contexto.TipoMaquinas
            .FirstOrDefault(tipo => tipo.IdTipoMaquina == id);
    }

    public TipoMaquina insertarTipoMaquina(TipoMaquina tipoMaquina)
    {
        contexto.TipoMaquinas.Add(tipoMaquina);
        contexto.SaveChanges();
        return tipoMaquina;
    }

    public bool actualizarTipoMaquina(TipoMaquina tipoMaquina)
    {
        var tipoExistente = contexto.TipoMaquinas
            .FirstOrDefault(tipo => tipo.IdTipoMaquina == tipoMaquina.IdTipoMaquina);
        if (tipoExistente is null) return false;

        tipoExistente.Nombre = tipoMaquina.Nombre;
        contexto.SaveChanges();
        return true;
    }

    public bool darBajaLogicaTipoMaquina(long id)
    {
        var tipoExistente = contexto.TipoMaquinas
            .FirstOrDefault(tipo => tipo.IdTipoMaquina == id);
        if (tipoExistente is null) return false;

        if (!tipoExistente.Estado) return true;

        tipoExistente.Estado = false;
        contexto.SaveChanges();
        return true;
    }
}
