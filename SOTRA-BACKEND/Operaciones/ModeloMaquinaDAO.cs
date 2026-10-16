using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class ModeloMaquinaDAO
{
    private readonly SotraContext _contexto;

    public ModeloMaquinaDAO() : this(new SotraContext())
    {
    }

    public ModeloMaquinaDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }


    public List<ModeloMaquina> ListarTodo()
    {
        return _contexto.ModeloMaquinas
            .OrderBy(modelo => modelo.IdModeloMaquina)
            .ToList();
    }

    public ModeloMaquina? SeleccionarModeloMaquina(long id)
    {
        return _contexto.ModeloMaquinas
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == id);
    }

    public ModeloMaquina InsertarModeloMaquina(ModeloMaquina modeloMaquina)
    {
        ValidarTipoMaquinaActivo(modeloMaquina.IdTipoMaquina);
        ValidarNombreDisponible(modeloMaquina.Nombre, modeloMaquina.IdTipoMaquina, null);
        _contexto.ModeloMaquinas.Add(modeloMaquina);
        _contexto.SaveChanges();
        return modeloMaquina;
    }

    public bool ActualizarModeloMaquina(ModeloMaquina modeloMaquina)
    {
        var modeloExistente = _contexto.ModeloMaquinas
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == modeloMaquina.IdModeloMaquina);
        if (modeloExistente is null) return false;

        ValidarTipoMaquinaActivo(modeloMaquina.IdTipoMaquina);
        ValidarNombreDisponible(modeloMaquina.Nombre, modeloMaquina.IdTipoMaquina, modeloExistente.IdModeloMaquina);
        modeloExistente.Nombre = modeloMaquina.Nombre;
        modeloExistente.IdTipoMaquina = modeloMaquina.IdTipoMaquina;
        modeloExistente.Descripcion = modeloMaquina.Descripcion;
        modeloExistente.Activo = modeloMaquina.Activo;

        _contexto.SaveChanges();
        return true;
    }

    public bool DarBajaLogicaModeloMaquina(long id)
    {
        var modeloExistente = _contexto.ModeloMaquinas
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == id);
        if (modeloExistente is null) return false;

        if (!modeloExistente.Activo) return true;

        modeloExistente.Activo = false;
        _contexto.SaveChanges();
        return true;
    }

    public bool EliminarModeloMaquina(long id)
    {
        var modeloExistente = _contexto.ModeloMaquinas
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == id);
        if (modeloExistente is null) return false;

        _contexto.ModeloMaquinas.Remove(modeloExistente);
        _contexto.SaveChanges();
        return true;
    }

    private void ValidarNombreDisponible(string nombre, long idTipoMaquina, long? idModeloActual)
    {
        var nombreNormalizado = nombre.Trim().ToLower();
        var existe = _contexto.ModeloMaquinas.AsNoTracking().Any(modelo =>
            modelo.IdTipoMaquina == idTipoMaquina
            && modelo.IdModeloMaquina != idModeloActual
            && modelo.Nombre.ToLower() == nombreNormalizado);
        if (existe)
            throw new ConflictoDeDatosException("MODELO_DUPLICADO",
                "Ya existe un modelo de máquina con ese nombre para el tipo seleccionado.");
    }

    private void ValidarTipoMaquinaActivo(long idTipoMaquina)
    {
        if (idTipoMaquina <= 0)
            throw new ArgumentException("El identificador del tipo de máquina debe ser positivo.");

        var tipo = _contexto.TipoMaquinas.AsNoTracking()
            .FirstOrDefault(tipo => tipo.IdTipoMaquina == idTipoMaquina);
        if (tipo is null)
            throw new ArgumentException("El tipo de máquina indicado no existe.");
        if (!tipo.Estado)
            throw new ArgumentException("El tipo de máquina indicado está inactivo.");
    }
}
