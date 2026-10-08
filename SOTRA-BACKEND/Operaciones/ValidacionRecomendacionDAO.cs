using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class ValidacionRecomendacionDAO
{
    private readonly SotraContext _contexto;

    public ValidacionRecomendacionDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }

    public List<ValidacionRecomendacionIa> ListarDeRecomendaciones(List<long> idsRecomendaciones)
    {
        return _contexto.ValidacionesRecomendacionIa
            .AsNoTracking()
            .Where(validacion => idsRecomendaciones.Contains(validacion.IdRecomendacionIa))
            .OrderBy(validacion => validacion.Fecha)
            .ThenBy(validacion => validacion.IdValidacion)
            .ToList();
    }

    public ValidacionRecomendacionIa Crear(ValidacionRecomendacionIa validacion)
    {
        validacion.Fecha = DateTime.UtcNow;
        _contexto.ValidacionesRecomendacionIa.Add(validacion);
        _contexto.SaveChanges();
        return validacion;
    }
}
