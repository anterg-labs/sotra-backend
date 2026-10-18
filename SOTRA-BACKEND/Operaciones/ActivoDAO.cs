using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class ActivoDAO
{
    private readonly SotraContext _contexto;

    public ActivoDAO() : this(new SotraContext())
    {
    }

    public ActivoDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }


    public async Task<List<Activo>> ListarTodoAsync(CancellationToken cancelacion)
    {
        return await _contexto.Activos
            .OrderBy(activo => activo.IdActivo)
            .ToListAsync(cancelacion);
    }

    public async Task<(List<Activo> Elementos, int Total)> ListarPaginaAsync(
        FiltroActivos filtro, int pagina, int tamanoPagina, CancellationToken cancelacion)
    {
        var consulta = _contexto.Activos.AsNoTracking()
            .Include(activo => activo.IdClienteNavigation)
            .Include(activo => activo.IdModeloMaquinaNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = ConsultasDeTexto.PatronContiene(filtro.Buscar);
            consulta = consulta.Where(a => EF.Functions.ILike(a.NumSerie, patron)
                                           || (a.Nombre != null && EF.Functions.ILike(a.Nombre, patron))
                                           || (a.Ubicacion != null && EF.Functions.ILike(a.Ubicacion, patron))
                                           || (a.Descripcion != null && EF.Functions.ILike(a.Descripcion, patron)));
        }

        if (filtro.ClienteId is { } clienteId)
        {
            consulta = consulta.Where(a => a.IdCliente == clienteId);
        }

        if (filtro.ModeloMaquinaId is { } modeloMaquinaId)
        {
            consulta = consulta.Where(a => a.IdModeloMaquina == modeloMaquinaId);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(a => a.Estado == estado);
        }

        return await ConsultasDeTexto.PaginarAsync(consulta.OrderBy(a => a.IdActivo), pagina, tamanoPagina, cancelacion);
    }

    public async Task<Activo?> SeleccionarActivoAsync(long id, CancellationToken cancelacion)
    {
        return await _contexto.Activos
            .Include(activo => activo.IdClienteNavigation)
            .Include(activo => activo.IdModeloMaquinaNavigation)
            .FirstOrDefaultAsync(activo => activo.IdActivo == id, cancelacion);
    }

    public async Task<Activo> InsertarActivoAsync(Activo activo, CancellationToken cancelacion)
    {
        await ValidarClienteActivoAsync(activo.IdCliente, cancelacion);
        await ValidarModeloMaquinaActivoAsync(activo.IdModeloMaquina, cancelacion);
        _contexto.Activos.Add(activo);
        await _contexto.SaveChangesAsync(cancelacion);
        return activo;
    }

    public async Task<bool> ActualizarActivoAsync(Activo activo, CancellationToken cancelacion)
    {
        var activoExistente = await _contexto.Activos
            .FirstOrDefaultAsync(a => a.IdActivo == activo.IdActivo, cancelacion);
        if (activoExistente is null) return false;

        await ValidarClienteActivoAsync(activo.IdCliente, cancelacion);
        await ValidarModeloMaquinaActivoAsync(activo.IdModeloMaquina, cancelacion);

        activoExistente.IdCliente = activo.IdCliente;
        activoExistente.NumSerie = activo.NumSerie;
        activoExistente.Descripcion = activo.Descripcion;
        activoExistente.IdModeloMaquina = activo.IdModeloMaquina;
        activoExistente.AnioFabricacion = activo.AnioFabricacion;
        activoExistente.Nombre = activo.Nombre;
        activoExistente.Ubicacion = activo.Ubicacion;
        activoExistente.Estado = activo.Estado;

        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> DarBajaLogicaActivoAsync(long id, CancellationToken cancelacion)
    {
        var activoExistente = await _contexto.Activos
            .FirstOrDefaultAsync(activo => activo.IdActivo == id, cancelacion);
        if (activoExistente is null) return false;

        if (!activoExistente.Estado) return true;

        activoExistente.Estado = false;
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> EliminarActivoAsync(long id, CancellationToken cancelacion)
    {
        var activoExistente = await _contexto.Activos
            .FirstOrDefaultAsync(activo => activo.IdActivo == id, cancelacion);
        if (activoExistente is null) return false;

        _contexto.Activos.Remove(activoExistente);
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    private async Task ValidarClienteActivoAsync(long idCliente, CancellationToken cancelacion)
    {
        if (idCliente <= 0)
            throw new ValidacionDeNegocioException("El identificador del cliente debe ser positivo.");

        var cliente = await _contexto.Clientes.AsNoTracking()
            .FirstOrDefaultAsync(cliente => cliente.IdCliente == idCliente, cancelacion);
        if (cliente is null)
            throw new ValidacionDeNegocioException("El cliente indicado no existe.");
        if (!cliente.Estado)
            throw new ValidacionDeNegocioException("El cliente indicado está inactivo.");
    }

    private async Task ValidarModeloMaquinaActivoAsync(long idModeloMaquina, CancellationToken cancelacion)
    {
        if (idModeloMaquina <= 0)
            throw new ValidacionDeNegocioException("El identificador del modelo de máquina debe ser positivo.");

        var modelo = await _contexto.ModeloMaquinas.AsNoTracking()
            .FirstOrDefaultAsync(modelo => modelo.IdModeloMaquina == idModeloMaquina, cancelacion);
        if (modelo is null)
            throw new ValidacionDeNegocioException("El modelo de máquina indicado no existe.");
        if (!modelo.Activo)
            throw new ValidacionDeNegocioException("El modelo de máquina indicado está inactivo.");
    }
}
