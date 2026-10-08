using Api.DTOs.Reportes;
using Api.DTOs.Repuestos;
using Api.Mapeadores;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/reportes")]
public class ReportesController : ControllerBase
{
    private const int CantidadDeActivosEnRanking = 5;

    private readonly OrdenTrabajoDAO _ordenTrabajoDao;
    private readonly RepuestoDAO _repuestoDao;

    public ReportesController(OrdenTrabajoDAO ordenTrabajoDao, RepuestoDAO repuestoDao)
    {
        _ordenTrabajoDao = ordenTrabajoDao;
        _repuestoDao = repuestoDao;
    }

    [HttpGet("indicadores")]
    public ActionResult<IndicadoresDto> ObtenerIndicadores()
    {
        var ordenesPorEstado = new List<CantidadPorGrupoDto>();
        var ordenesPorPrioridad = new List<CantidadPorGrupoDto>();
        var ordenesPorTipo = new List<CantidadPorGrupoDto>();
        var ordenesPorTecnico = new List<CantidadPorGrupoDto>();
        var intervencionesPorActivo = new List<CantidadPorGrupoDto>();
        var totalOrdenes = 0;

        foreach (var ordenTrabajo in _ordenTrabajoDao.ListarTodo())
        {
            var orden = OrdenTrabajoMapeador.ADto(ordenTrabajo);
            totalOrdenes = totalOrdenes + 1;

            SumarUno(ordenesPorEstado, orden.Estado);
            SumarUno(ordenesPorPrioridad, orden.Prioridad);
            SumarUno(ordenesPorTipo, orden.Tipo);
            SumarUno(intervencionesPorActivo, orden.NombreActivo);

            if (orden.NombreTecnico is not null)
            {
                SumarUno(ordenesPorTecnico, orden.NombreTecnico);
            }
        }

        var activosConMasIntervenciones = intervencionesPorActivo
            .OrderByDescending(grupo => grupo.Cantidad)
            .Take(CantidadDeActivosEnRanking)
            .ToList();

        var repuestosBajoStock = new List<RepuestoDto>();
        foreach (var repuesto in _repuestoDao.ListarTodo())
        {
            var repuestoDto = RepuestoMapeador.ADto(repuesto);
            if (repuestoDto.Estado == "activo" && repuestoDto.Cantidad <= repuestoDto.CantidadMinima)
            {
                repuestosBajoStock.Add(repuestoDto);
            }
        }

        return Ok(new IndicadoresDto
        {
            TotalOrdenes = totalOrdenes,
            OrdenesPorEstado = ordenesPorEstado,
            OrdenesPorPrioridad = ordenesPorPrioridad,
            OrdenesPorTipo = ordenesPorTipo,
            OrdenesPorTecnico = ordenesPorTecnico,
            ActivosConMasIntervenciones = activosConMasIntervenciones,
            RepuestosBajoStock = repuestosBajoStock
        });
    }

    private static void SumarUno(List<CantidadPorGrupoDto> grupos, string nombre)
    {
        foreach (var grupo in grupos)
        {
            if (grupo.Nombre == nombre)
            {
                grupo.Cantidad = grupo.Cantidad + 1;
                return;
            }
        }

        grupos.Add(new CantidadPorGrupoDto { Nombre = nombre, Cantidad = 1 });
    }
}
