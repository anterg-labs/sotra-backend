using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class EvidenciaOtDAO
{
    private readonly SotraContext _contexto;

    public EvidenciaOtDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<List<EvidenciaOt>> ListarDeOrdenAsync(long idOrden, CancellationToken cancelacion)
    {
        return await _contexto.EvidenciasOt
            .AsNoTracking()
            .Where(evidencia => evidencia.IdOrden == idOrden)
            .OrderBy(evidencia => evidencia.IdEvidencia)
            .Select(evidencia => new EvidenciaOt
            {
                IdEvidencia = evidencia.IdEvidencia,
                IdOrden = evidencia.IdOrden,
                IdUsuario = evidencia.IdUsuario,
                Tipo = evidencia.Tipo,
                Observacion = evidencia.Observacion,
                FechaRegistro = evidencia.FechaRegistro,
                CreadoEn = evidencia.CreadoEn,
                NombreArchivo = evidencia.NombreArchivo,
                TipoContenido = evidencia.TipoContenido
            })
            .ToListAsync(cancelacion);
    }

    public async Task<EvidenciaOt?> SeleccionarAsync(long idOrden, long idEvidencia, CancellationToken cancelacion)
    {
        return await _contexto.EvidenciasOt
            .AsNoTracking()
            .FirstOrDefaultAsync(evidencia => evidencia.IdOrden == idOrden && evidencia.IdEvidencia == idEvidencia, cancelacion);
    }

    public async Task<EvidenciaOt> CrearAsync(EvidenciaOt evidencia, CancellationToken cancelacion)
    {
        evidencia.CreadoEn = DateTime.UtcNow;
        _contexto.EvidenciasOt.Add(evidencia);
        await _contexto.SaveChangesAsync(cancelacion);
        return evidencia;
    }
}
