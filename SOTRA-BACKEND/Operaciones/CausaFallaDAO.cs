using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

/// <summary>Acceso al catálogo de causas de falla.</summary>
public class CausaFallaDAO(SotraContext contexto)
{
    public async Task<List<CausaFalla>> ListarAsync(bool? estado, CancellationToken cancelacion)
    {
        var consulta = contexto.CausasFalla.AsNoTracking();
        if (estado is not null)
        {
            consulta = consulta.Where(causa => causa.Estado == estado);
        }

        return await consulta.OrderBy(causa => causa.Nombre).ToListAsync(cancelacion);
    }

    public async Task<CausaFalla?> SeleccionarAsync(long id, CancellationToken cancelacion) =>
        await contexto.CausasFalla.FirstOrDefaultAsync(causa => causa.IdCausaFalla == id, cancelacion);

    public async Task<CausaFalla> InsertarAsync(string nombre, CancellationToken cancelacion)
    {
        await ValidarNombreAsync(nombre, null, cancelacion);
        var causa = new CausaFalla { Nombre = nombre, Estado = true };
        contexto.CausasFalla.Add(causa);
        await contexto.SaveChangesAsync(cancelacion);
        return causa;
    }

    public async Task<bool> ActualizarNombreAsync(long id, string nombre, CancellationToken cancelacion)
    {
        var causa = await SeleccionarAsync(id, cancelacion);
        if (causa is null)
        {
            return false;
        }

        await ValidarNombreAsync(nombre, id, cancelacion);
        causa.Nombre = nombre;
        await contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<CausaFalla?> CambiarEstadoAsync(long id, bool estado, CancellationToken cancelacion)
    {
        var causa = await SeleccionarAsync(id, cancelacion);
        if (causa is null)
        {
            return null;
        }

        causa.Estado = estado;
        await contexto.SaveChangesAsync(cancelacion);
        return causa;
    }

    private async Task ValidarNombreAsync(string nombre, long? idPropio, CancellationToken cancelacion)
    {
        var minuscula = nombre.ToLower();
        var existe = await contexto.CausasFalla.AsNoTracking()
            .AnyAsync(causa => causa.Nombre.ToLower() == minuscula && causa.IdCausaFalla != idPropio, cancelacion);
        if (existe)
        {
            throw new ConflictoDeDatosException("CAUSA_FALLA_DUPLICADA", "Ya existe una causa de falla con ese nombre.");
        }
    }
}
