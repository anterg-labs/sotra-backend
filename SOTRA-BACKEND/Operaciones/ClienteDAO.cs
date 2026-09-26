using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class ClienteDAO
{
    private readonly SotraContext contexto;

    public ClienteDAO() : this(new SotraContext())
    {
    }

    public ClienteDAO(SotraContext contexto)
    {
        this.contexto = contexto;
    }

    public Task<List<T>> ListarTodoAsync<T>(
        Expression<Func<Cliente, T>> proyeccion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(proyeccion);
        return contexto.Clientes
            .AsNoTracking()
            .OrderBy(cliente => cliente.IdCliente)
            .Select(proyeccion)
            .ToListAsync(cancellationToken);
    }

    public List<Cliente> listarTodo()
    {
        return contexto.Clientes
            .Include(cliente => cliente.Activos)
            .OrderBy(cliente => cliente.IdCliente)
            .ToList();
    }

    public Cliente? seleccionarCliente(long id)
    {
        return contexto.Clientes
            .Include(cliente => cliente.Activos)
            .FirstOrDefault(cliente => cliente.IdCliente == id);
    }

    public Cliente insertarCliente(Cliente cliente)
    {
        contexto.Clientes.Add(cliente);
        contexto.SaveChanges();
        return cliente;
    }

    public bool actualizarCliente(Cliente cliente)
    {
        var clienteExistente = contexto.Clientes
            .FirstOrDefault(c => c.IdCliente == cliente.IdCliente);
        if (clienteExistente is null) return false;

        clienteExistente.RazonSocial = cliente.RazonSocial;
        clienteExistente.Nombre = cliente.Nombre;
        clienteExistente.Correo = cliente.Correo;
        clienteExistente.Telefono = cliente.Telefono;
        clienteExistente.Direccion = cliente.Direccion;
        clienteExistente.Observaciones = cliente.Observaciones;

        contexto.SaveChanges();
        return true;
    }

    public bool darBajaLogicaCliente(long id)
    {
        var clienteExistente = contexto.Clientes
            .FirstOrDefault(cliente => cliente.IdCliente == id);
        if (clienteExistente is null) return false;

        if (!clienteExistente.Estado) return true;

        clienteExistente.Estado = false;
        contexto.SaveChanges();
        return true;
    }

    public bool eliminarCliente(long id)
    {
        var clienteExistente = contexto.Clientes
            .FirstOrDefault(cliente => cliente.IdCliente == id);
        if (clienteExistente is null) return false;

        contexto.Clientes.Remove(clienteExistente);
        contexto.SaveChanges();
        return true;
    }
}
