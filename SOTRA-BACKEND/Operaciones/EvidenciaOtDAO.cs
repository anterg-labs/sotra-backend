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

    public List<EvidenciaOt> ListarDeOrden(long idOrden)
    {
        return _contexto.EvidenciasOt
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
            .ToList();
    }

    public EvidenciaOt? Seleccionar(long idOrden, long idEvidencia)
    {
        return _contexto.EvidenciasOt
            .AsNoTracking()
            .FirstOrDefault(evidencia => evidencia.IdOrden == idOrden && evidencia.IdEvidencia == idEvidencia);
    }

    public EvidenciaOt Crear(EvidenciaOt evidencia)
    {
        evidencia.CreadoEn = DateTime.UtcNow;
        _contexto.EvidenciasOt.Add(evidencia);
        _contexto.SaveChanges();
        return evidencia;
    }
}
