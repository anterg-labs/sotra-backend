using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class ActivoDAO
{
    private readonly SotraContext contexto;

    public ActivoDAO() : this(new SotraContext())
    {
    }

    public ActivoDAO(SotraContext contexto)
    {
        this.contexto = contexto;
    }

    public Task<List<T>> ListarTodoAsync<T>(
        Expression<Func<Activo, T>> proyeccion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proyeccion);
        return contexto.Activos.AsNoTracking()
            .OrderBy(activo => activo.IdActivo)
            .Select(proyeccion)
            .ToListAsync(cancellationToken);
    }

    public List<Activo> listarTodo()
    {
        return contexto.Activos
            .OrderBy(activo => activo.IdActivo)
            .ToList();
    }

    public Activo? seleccionarActivo(long id)
    {
        return contexto.Activos
            .FirstOrDefault(activo => activo.IdActivo == id);
    }

    public Activo insertarActivo(Activo activo)
    {
        ValidarClienteActivo(activo.IdCliente);
        ValidarModeloMaquinaActivo(activo.IdModeloMaquina);
        contexto.Activos.Add(activo);
        contexto.SaveChanges();
        return activo;
    }

    public bool actualizarActivo(Activo activo)
    {
        var activoExistente = contexto.Activos
            .FirstOrDefault(a => a.IdActivo == activo.IdActivo);
        if (activoExistente is null) return false;

        ValidarClienteActivo(activo.IdCliente);
        ValidarModeloMaquinaActivo(activo.IdModeloMaquina);

        activoExistente.IdCliente = activo.IdCliente;
        activoExistente.NumSerie = activo.NumSerie;
        activoExistente.Descripcion = activo.Descripcion;
        activoExistente.IdModeloMaquina = activo.IdModeloMaquina;
        activoExistente.AnioFabricacion = activo.AnioFabricacion;
        activoExistente.Nombre = activo.Nombre;
        activoExistente.Ubicacion = activo.Ubicacion;

        contexto.SaveChanges();
        return true;
    }

    public bool darBajaLogicaActivo(long id)
    {
        var activoExistente = contexto.Activos
            .FirstOrDefault(activo => activo.IdActivo == id);
        if (activoExistente is null) return false;

        if (!activoExistente.Estado) return true;

        activoExistente.Estado = false;
        contexto.SaveChanges();
        return true;
    }

    public bool eliminarActivo(long id)
    {
        var activoExistente = contexto.Activos
            .FirstOrDefault(activo => activo.IdActivo == id);
        if (activoExistente is null) return false;

        contexto.Activos.Remove(activoExistente);
        contexto.SaveChanges();
        return true;
    }

    private void ValidarClienteActivo(long idCliente)
    {
        if (idCliente <= 0)
            throw new ArgumentException("El identificador del cliente debe ser positivo.", nameof(idCliente));

        var cliente = contexto.Clientes.AsNoTracking()
            .FirstOrDefault(cliente => cliente.IdCliente == idCliente);
        if (cliente is null)
            throw new ArgumentException("El cliente indicado no existe.", nameof(idCliente));
        if (!cliente.Estado)
            throw new ArgumentException("El cliente indicado está inactivo.", nameof(idCliente));
    }

    private void ValidarModeloMaquinaActivo(long idModeloMaquina)
    {
        if (idModeloMaquina <= 0)
            throw new ArgumentException("El identificador del modelo de máquina debe ser positivo.", nameof(idModeloMaquina));

        var modelo = contexto.ModeloMaquinas.AsNoTracking()
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == idModeloMaquina);
        if (modelo is null)
            throw new ArgumentException("El modelo de máquina indicado no existe.", nameof(idModeloMaquina));
        if (!modelo.Activo)
            throw new ArgumentException("El modelo de máquina indicado está inactivo.", nameof(idModeloMaquina));
    }
}
