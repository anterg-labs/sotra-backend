using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class ModeloMaquinaDAO
{
    private readonly SotraContext contexto;

    public ModeloMaquinaDAO() : this(new SotraContext())
    {
    }

    public ModeloMaquinaDAO(SotraContext contexto)
    {
        this.contexto = contexto;
    }

    public Task<List<T>> ListarTodoAsync<T>(
        Expression<Func<ModeloMaquina, T>> proyeccion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proyeccion);
        return contexto.ModeloMaquinas.AsNoTracking()
            .OrderBy(modelo => modelo.IdModeloMaquina)
            .Select(proyeccion)
            .ToListAsync(cancellationToken);
    }

    public List<ModeloMaquina> listarTodo()
    {
        return contexto.ModeloMaquinas
            .OrderBy(modelo => modelo.IdModeloMaquina)
            .ToList();
    }

    public ModeloMaquina? seleccionarModeloMaquina(long id)
    {
        return contexto.ModeloMaquinas
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == id);
    }

    public ModeloMaquina insertarModeloMaquina(ModeloMaquina modeloMaquina)
    {
        ValidarTipoMaquinaActivo(modeloMaquina.IdTipoMaquina);
        contexto.ModeloMaquinas.Add(modeloMaquina);
        contexto.SaveChanges();
        return modeloMaquina;
    }

    public bool actualizarModeloMaquina(ModeloMaquina modeloMaquina)
    {
        var modeloExistente = contexto.ModeloMaquinas
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == modeloMaquina.IdModeloMaquina);
        if (modeloExistente is null) return false;

        ValidarTipoMaquinaActivo(modeloMaquina.IdTipoMaquina);
        modeloExistente.Nombre = modeloMaquina.Nombre;
        modeloExistente.IdTipoMaquina = modeloMaquina.IdTipoMaquina;
        modeloExistente.Descripcion = modeloMaquina.Descripcion;
        modeloExistente.Activo = modeloMaquina.Activo;

        contexto.SaveChanges();
        return true;
    }

    public bool darBajaLogicaModeloMaquina(long id)
    {
        var modeloExistente = contexto.ModeloMaquinas
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == id);
        if (modeloExistente is null) return false;

        if (!modeloExistente.Activo) return true;

        modeloExistente.Activo = false;
        contexto.SaveChanges();
        return true;
    }

    // Se conserva para la consola de pruebas existente.
    public bool eliminarModeloMaquina(long id)
    {
        var modeloExistente = contexto.ModeloMaquinas
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == id);
        if (modeloExistente is null) return false;

        contexto.ModeloMaquinas.Remove(modeloExistente);
        contexto.SaveChanges();
        return true;
    }

    private void ValidarTipoMaquinaActivo(long idTipoMaquina)
    {
        if (idTipoMaquina <= 0)
            throw new ArgumentException("El identificador del tipo de máquina debe ser positivo.", nameof(idTipoMaquina));

        var tipo = contexto.TipoMaquinas.AsNoTracking()
            .FirstOrDefault(tipo => tipo.IdTipoMaquina == idTipoMaquina);
        if (tipo is null)
            throw new ArgumentException("El tipo de máquina indicado no existe.", nameof(idTipoMaquina));
        if (!tipo.Estado)
            throw new ArgumentException("El tipo de máquina indicado está inactivo.", nameof(idTipoMaquina));
    }
}
