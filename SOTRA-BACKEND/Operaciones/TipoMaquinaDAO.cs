using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class TipoMaquinaDAO
{
    private readonly SotraContext _contexto;

    public TipoMaquinaDAO() : this(new SotraContext())
    {
    }

    public TipoMaquinaDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }


    public List<TipoMaquina> ListarTodo()
    {
        return _contexto.TipoMaquinas
            .OrderBy(tipo => tipo.IdTipoMaquina)
            .ToList();
    }

    public TipoMaquina? SeleccionarTipoMaquina(long id)
    {
        return _contexto.TipoMaquinas
            .FirstOrDefault(tipo => tipo.IdTipoMaquina == id);
    }

    public TipoMaquina InsertarTipoMaquina(TipoMaquina tipoMaquina)
    {
        _contexto.TipoMaquinas.Add(tipoMaquina);
        _contexto.SaveChanges();
        return tipoMaquina;
    }

    public bool ActualizarTipoMaquina(TipoMaquina tipoMaquina)
    {
        var tipoExistente = _contexto.TipoMaquinas
            .FirstOrDefault(tipo => tipo.IdTipoMaquina == tipoMaquina.IdTipoMaquina);
        if (tipoExistente is null) return false;

        tipoExistente.Nombre = tipoMaquina.Nombre;
        tipoExistente.Estado = tipoMaquina.Estado;
        _contexto.SaveChanges();
        return true;
    }

    public bool DarBajaLogicaTipoMaquina(long id)
    {
        var tipoExistente = _contexto.TipoMaquinas
            .FirstOrDefault(tipo => tipo.IdTipoMaquina == id);
        if (tipoExistente is null) return false;

        if (!tipoExistente.Estado) return true;

        tipoExistente.Estado = false;
        _contexto.SaveChanges();
        return true;
    }
}
