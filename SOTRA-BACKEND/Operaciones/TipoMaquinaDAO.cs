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


    public async Task<List<TipoMaquina>> ListarTodoAsync(CancellationToken cancelacion)
    {
        return await _contexto.TipoMaquinas
            .OrderBy(tipo => tipo.IdTipoMaquina)
            .ToListAsync(cancelacion);
    }

    public async Task<(List<TipoMaquina> Elementos, int Total)> ListarPaginaAsync(
        FiltroTiposMaquina filtro, int pagina, int tamanoPagina, CancellationToken cancelacion)
    {
        var consulta = _contexto.TipoMaquinas.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = ConsultasDeTexto.PatronContiene(filtro.Buscar);
            consulta = consulta.Where(t => EF.Functions.ILike(t.Nombre, patron));
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(t => t.Estado == estado);
        }

        return await ConsultasDeTexto.PaginarAsync(
            consulta.OrderBy(t => t.IdTipoMaquina), pagina, tamanoPagina, cancelacion);
    }

    public async Task<TipoMaquina?> SeleccionarTipoMaquinaAsync(long id, CancellationToken cancelacion)
    {
        return await _contexto.TipoMaquinas
            .FirstOrDefaultAsync(tipo => tipo.IdTipoMaquina == id, cancelacion);
    }

    public async Task<TipoMaquina> InsertarTipoMaquinaAsync(TipoMaquina tipoMaquina, CancellationToken cancelacion)
    {
        _contexto.TipoMaquinas.Add(tipoMaquina);
        await _contexto.SaveChangesAsync(cancelacion);
        return tipoMaquina;
    }

    public async Task<bool> ActualizarTipoMaquinaAsync(TipoMaquina tipoMaquina, CancellationToken cancelacion)
    {
        var tipoExistente = await _contexto.TipoMaquinas
            .FirstOrDefaultAsync(tipo => tipo.IdTipoMaquina == tipoMaquina.IdTipoMaquina, cancelacion);
        if (tipoExistente is null) return false;

        tipoExistente.Nombre = tipoMaquina.Nombre;
        tipoExistente.Estado = tipoMaquina.Estado;
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> DarBajaLogicaTipoMaquinaAsync(long id, CancellationToken cancelacion)
    {
        var tipoExistente = await _contexto.TipoMaquinas
            .FirstOrDefaultAsync(tipo => tipo.IdTipoMaquina == id, cancelacion);
        if (tipoExistente is null) return false;

        if (!tipoExistente.Estado) return true;

        tipoExistente.Estado = false;
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }
}
