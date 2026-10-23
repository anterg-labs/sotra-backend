using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class ModeloMaquinaDAO
{
    public static readonly ColumnasOrdenables<ModeloMaquina> ColumnasOrdenables = new(
        m => m.IdModeloMaquina,
        [
            new("nombre", m => m.Nombre),
            new("nombreTipoMaquina", m => m.IdTipoMaquinaNavigation.Nombre),
            new("descripcion", m => m.Descripcion),
            new("estado", m => m.Activo),
        ]);

    private readonly SotraContext _contexto;

    public ModeloMaquinaDAO() : this(new SotraContext())
    {
    }

    public ModeloMaquinaDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }


    public async Task<List<ModeloMaquina>> ListarTodoAsync(CancellationToken cancelacion)
    {
        return await _contexto.ModeloMaquinas
            .OrderBy(modelo => modelo.IdModeloMaquina)
            .ToListAsync(cancelacion);
    }

    public async Task<(List<ModeloMaquina> Elementos, int Total)> ListarPaginaAsync(
        FiltroModelosMaquina filtro, int pagina, int tamanoPagina, CancellationToken cancelacion)
    {
        var consulta = _contexto.ModeloMaquinas.AsNoTracking()
            .Include(modelo => modelo.IdTipoMaquinaNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = ConsultasDeTexto.PatronContiene(filtro.Buscar);
            consulta = consulta.Where(m => EF.Functions.ILike(m.Nombre, patron)
                                           || (m.Descripcion != null && EF.Functions.ILike(m.Descripcion, patron)));
        }

        if (filtro.TipoMaquinaId is { } tipoMaquinaId)
        {
            consulta = consulta.Where(m => m.IdTipoMaquina == tipoMaquinaId);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(m => m.Activo == estado);
        }

        return await ConsultasDeTexto.PaginarAsync(
            ColumnasOrdenables.Aplicar(consulta, filtro.OrdenarPor, filtro.Descendente), pagina, tamanoPagina, cancelacion);
    }

    public async Task<ModeloMaquina?> SeleccionarModeloMaquinaAsync(long id, CancellationToken cancelacion)
    {
        return await _contexto.ModeloMaquinas
            .Include(modelo => modelo.IdTipoMaquinaNavigation)
            .FirstOrDefaultAsync(modelo => modelo.IdModeloMaquina == id, cancelacion);
    }

    public async Task<ModeloMaquina> InsertarModeloMaquinaAsync(ModeloMaquina modeloMaquina, CancellationToken cancelacion)
    {
        await ValidarTipoMaquinaActivoAsync(modeloMaquina.IdTipoMaquina, cancelacion);
        await ValidarNombreDisponibleAsync(modeloMaquina.Nombre, modeloMaquina.IdTipoMaquina, null, cancelacion);
        _contexto.ModeloMaquinas.Add(modeloMaquina);
        await _contexto.SaveChangesAsync(cancelacion);
        return modeloMaquina;
    }

    public async Task<bool> ActualizarModeloMaquinaAsync(ModeloMaquina modeloMaquina, CancellationToken cancelacion)
    {
        var modeloExistente = await _contexto.ModeloMaquinas
            .FirstOrDefaultAsync(modelo => modelo.IdModeloMaquina == modeloMaquina.IdModeloMaquina, cancelacion);
        if (modeloExistente is null) return false;

        await ValidarTipoMaquinaActivoAsync(modeloMaquina.IdTipoMaquina, cancelacion);
        await ValidarNombreDisponibleAsync(
            modeloMaquina.Nombre, modeloMaquina.IdTipoMaquina, modeloExistente.IdModeloMaquina, cancelacion);
        modeloExistente.Nombre = modeloMaquina.Nombre;
        modeloExistente.IdTipoMaquina = modeloMaquina.IdTipoMaquina;
        modeloExistente.Descripcion = modeloMaquina.Descripcion;
        modeloExistente.Activo = modeloMaquina.Activo;

        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> DarBajaLogicaModeloMaquinaAsync(long id, CancellationToken cancelacion)
    {
        var modeloExistente = await _contexto.ModeloMaquinas
            .FirstOrDefaultAsync(modelo => modelo.IdModeloMaquina == id, cancelacion);
        if (modeloExistente is null) return false;

        if (!modeloExistente.Activo) return true;

        modeloExistente.Activo = false;
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> EliminarModeloMaquinaAsync(long id, CancellationToken cancelacion)
    {
        var modeloExistente = await _contexto.ModeloMaquinas
            .FirstOrDefaultAsync(modelo => modelo.IdModeloMaquina == id, cancelacion);
        if (modeloExistente is null) return false;

        _contexto.ModeloMaquinas.Remove(modeloExistente);
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    private async Task ValidarNombreDisponibleAsync(
        string nombre, long idTipoMaquina, long? idModeloActual, CancellationToken cancelacion)
    {
        var nombreNormalizado = nombre.Trim().ToLower();
        var existe = await _contexto.ModeloMaquinas.AsNoTracking().AnyAsync(modelo =>
            modelo.IdTipoMaquina == idTipoMaquina
            && modelo.IdModeloMaquina != idModeloActual
            && modelo.Nombre.ToLower() == nombreNormalizado, cancelacion);
        if (existe)
            throw new ConflictoDeDatosException("MODELO_DUPLICADO",
                "Ya existe un modelo de máquina con ese nombre para el tipo seleccionado.");
    }

    private async Task ValidarTipoMaquinaActivoAsync(long idTipoMaquina, CancellationToken cancelacion)
    {
        if (idTipoMaquina <= 0)
            throw new ValidacionDeNegocioException("El identificador del tipo de máquina debe ser positivo.");

        var tipo = await _contexto.TipoMaquinas.AsNoTracking()
            .FirstOrDefaultAsync(tipo => tipo.IdTipoMaquina == idTipoMaquina, cancelacion);
        if (tipo is null)
            throw new ValidacionDeNegocioException("El tipo de máquina indicado no existe.");
        if (!tipo.Estado)
            throw new ValidacionDeNegocioException("El tipo de máquina indicado está inactivo.");
    }
}
