using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class ClienteDAO
{
    private readonly SotraContext _contexto;

    public ClienteDAO() : this(new SotraContext())
    {
    }

    public ClienteDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }


    public List<Cliente> ListarTodo()
    {
        return _contexto.Clientes
            .Include(cliente => cliente.Activos)
            .OrderBy(cliente => cliente.IdCliente)
            .ToList();
    }

    public (List<Cliente> Elementos, int Total) ListarPagina(FiltroClientes filtro, int pagina, int tamanoPagina)
    {
        var consulta = _contexto.Clientes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = ConsultasDeTexto.PatronContiene(filtro.Buscar);
            consulta = consulta.Where(c => EF.Functions.ILike(c.RazonSocial, patron)
                                           || EF.Functions.ILike(c.Nombre, patron)
                                           || (c.Correo != null && EF.Functions.ILike(c.Correo, patron)));
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(c => c.Estado == estado);
        }

        var ordenada = filtro.Descendente
            ? consulta.OrderByDescending(c => c.IdCliente)
            : consulta.OrderBy(c => c.IdCliente);

        return ConsultasDeTexto.Paginar(ordenada.Include(c => c.Activos), pagina, tamanoPagina);
    }

    public Cliente? SeleccionarCliente(long id)
    {
        return _contexto.Clientes
            .Include(cliente => cliente.Activos)
            .FirstOrDefault(cliente => cliente.IdCliente == id);
    }

    public Cliente InsertarCliente(Cliente cliente)
    {
        _contexto.Clientes.Add(cliente);
        _contexto.SaveChanges();
        return cliente;
    }

    public bool ActualizarCliente(Cliente cliente)
    {
        var clienteExistente = _contexto.Clientes
            .FirstOrDefault(c => c.IdCliente == cliente.IdCliente);
        if (clienteExistente is null) return false;

        clienteExistente.RazonSocial = cliente.RazonSocial;
        clienteExistente.Nombre = cliente.Nombre;
        clienteExistente.Correo = cliente.Correo;
        clienteExistente.Telefono = cliente.Telefono;
        clienteExistente.Direccion = cliente.Direccion;
        clienteExistente.Observaciones = cliente.Observaciones;

        _contexto.SaveChanges();
        return true;
    }

    public bool DarBajaLogicaCliente(long id)
    {
        var clienteExistente = _contexto.Clientes
            .FirstOrDefault(cliente => cliente.IdCliente == id);
        if (clienteExistente is null) return false;

        if (!clienteExistente.Estado) return true;

        clienteExistente.Estado = false;
        _contexto.SaveChanges();
        return true;
    }

    public bool ReactivarCliente(long id)
    {
        var clienteExistente = _contexto.Clientes
            .FirstOrDefault(cliente => cliente.IdCliente == id);
        if (clienteExistente is null) return false;

        clienteExistente.Estado = true;
        _contexto.SaveChanges();
        return true;
    }

    public bool EliminarCliente(long id)
    {
        var clienteExistente = _contexto.Clientes
            .FirstOrDefault(cliente => cliente.IdCliente == id);
        if (clienteExistente is null) return false;

        _contexto.Clientes.Remove(clienteExistente);
        _contexto.SaveChanges();
        return true;
    }
}
