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


    public async Task<List<Cliente>> ListarTodoAsync(CancellationToken cancelacion)
    {
        return await _contexto.Clientes
            .Include(cliente => cliente.Activos)
            .OrderBy(cliente => cliente.IdCliente)
            .ToListAsync(cancelacion);
    }

    public async Task<(List<Cliente> Elementos, int Total)> ListarPaginaAsync(
        FiltroClientes filtro, int pagina, int tamanoPagina, CancellationToken cancelacion)
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

        return await ConsultasDeTexto.PaginarAsync(
            ordenada.Include(c => c.Activos), pagina, tamanoPagina, cancelacion);
    }

    public async Task<Cliente?> SeleccionarClienteAsync(long id, CancellationToken cancelacion)
    {
        return await _contexto.Clientes
            .Include(cliente => cliente.Activos)
            .FirstOrDefaultAsync(cliente => cliente.IdCliente == id, cancelacion);
    }

    public async Task<Cliente> InsertarClienteAsync(Cliente cliente, CancellationToken cancelacion)
    {
        _contexto.Clientes.Add(cliente);
        await _contexto.SaveChangesAsync(cancelacion);
        return cliente;
    }

    public async Task<bool> ActualizarClienteAsync(Cliente cliente, CancellationToken cancelacion)
    {
        var clienteExistente = await _contexto.Clientes
            .FirstOrDefaultAsync(c => c.IdCliente == cliente.IdCliente, cancelacion);
        if (clienteExistente is null) return false;

        clienteExistente.RazonSocial = cliente.RazonSocial;
        clienteExistente.Nombre = cliente.Nombre;
        clienteExistente.Correo = cliente.Correo;
        clienteExistente.Telefono = cliente.Telefono;
        clienteExistente.Direccion = cliente.Direccion;
        clienteExistente.Observaciones = cliente.Observaciones;

        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> DarBajaLogicaClienteAsync(long id, CancellationToken cancelacion)
    {
        var clienteExistente = await _contexto.Clientes
            .FirstOrDefaultAsync(cliente => cliente.IdCliente == id, cancelacion);
        if (clienteExistente is null) return false;

        if (!clienteExistente.Estado) return true;

        clienteExistente.Estado = false;
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> ReactivarClienteAsync(long id, CancellationToken cancelacion)
    {
        var clienteExistente = await _contexto.Clientes
            .FirstOrDefaultAsync(cliente => cliente.IdCliente == id, cancelacion);
        if (clienteExistente is null) return false;

        clienteExistente.Estado = true;
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> EliminarClienteAsync(long id, CancellationToken cancelacion)
    {
        var clienteExistente = await _contexto.Clientes
            .FirstOrDefaultAsync(cliente => cliente.IdCliente == id, cancelacion);
        if (clienteExistente is null) return false;

        _contexto.Clientes.Remove(clienteExistente);
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }
}
