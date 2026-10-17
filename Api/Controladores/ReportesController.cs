using Api.DTOs.OrdenesTrabajo;
using Api.DTOs.Reportes;
using Api.DTOs.Repuestos;
using Api.Mapeadores;
using Api.Seguridad;
using Api.Servicios;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SOTRA_BACKEND.Operaciones;

namespace Api.Controladores;

[ApiController]
[Route("api/v1/reportes")]
public class ReportesController : ControllerBase
{
    private const int CantidadDeActivosEnRanking = 5;
    private const int CantidadDeOrdenesRecientes = 5;

    private readonly OrdenTrabajoDAO _ordenTrabajoDao;
    private readonly RepuestoDAO _repuestoDao;

    public ReportesController(OrdenTrabajoDAO ordenTrabajoDao, RepuestoDAO repuestoDao)
    {
        _ordenTrabajoDao = ordenTrabajoDao;
        _repuestoDao = repuestoDao;
    }

    [Authorize(Policy = Politicas.ReporteConsultar)]
    [HttpGet("indicadores")]
    public ActionResult<IndicadoresDto> ObtenerIndicadores([FromQuery] FiltrosIndicadoresDto filtros)
    {
        if (filtros.FechaDesde is not null && filtros.FechaHasta is not null
            && filtros.FechaDesde > filtros.FechaHasta)
        {
            return BadRequest(new
            {
                codigo = "RANGO_FECHAS_INVALIDO",
                mensaje = "La fecha desde no puede ser posterior a la fecha hasta."
            });
        }

        var ordenes = ListarOrdenesFiltradas(filtros);

        var ordenesPorEstado = new List<CantidadPorGrupoDto>();
        var ordenesPorPrioridad = new List<CantidadPorGrupoDto>();
        var ordenesPorTipo = new List<CantidadPorGrupoDto>();
        var ordenesPorTecnico = new List<CantidadPorGrupoDto>();
        var intervencionesPorActivo = new List<CantidadPorGrupoDto>();
        var ordenesAltaPrioridadPendientes = 0;
        var ordenesPendientesDeValidacion = 0;
        var horasHastaInicio = new List<double>();
        var horasHastaCierre = new List<double>();

        foreach (var orden in ordenes)
        {
            SumarUno(ordenesPorEstado, orden.Estado);
            SumarUno(ordenesPorPrioridad, orden.Prioridad);
            SumarUno(ordenesPorTipo, orden.Tipo);
            SumarUno(intervencionesPorActivo, orden.NombreActivo);

            if (orden.NombreTecnico is not null)
            {
                SumarUno(ordenesPorTecnico, orden.NombreTecnico);
            }

            var estaTerminada = orden.Estado == "finalizada" || orden.Estado == "cerrada" || orden.Estado == "cancelada";
            if (orden.Prioridad == "alta" && !estaTerminada)
            {
                ordenesAltaPrioridadPendientes = ordenesAltaPrioridadPendientes + 1;
            }

            if (orden.Estado == "finalizada")
            {
                ordenesPendientesDeValidacion = ordenesPendientesDeValidacion + 1;
            }

            if (orden.FechaInicio is not null)
            {
                horasHastaInicio.Add((orden.FechaInicio.Value - orden.CreadoEn).TotalHours);
            }

            if (orden.FechaCierre is not null)
            {
                horasHastaCierre.Add((orden.FechaCierre.Value - orden.CreadoEn).TotalHours);
            }
        }

        var activosConMasIntervenciones = intervencionesPorActivo
            .OrderByDescending(grupo => grupo.Cantidad)
            .Take(CantidadDeActivosEnRanking)
            .ToList();

        var ordenesRecientes = ordenes
            .OrderByDescending(orden => orden.CreadoEn)
            .Take(CantidadDeOrdenesRecientes)
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
            TotalOrdenes = ordenes.Count,
            OrdenesPorEstado = ordenesPorEstado,
            OrdenesPorPrioridad = ordenesPorPrioridad,
            OrdenesPorTipo = ordenesPorTipo,
            OrdenesPorTecnico = ordenesPorTecnico,
            ActivosConMasIntervenciones = activosConMasIntervenciones,
            RepuestosBajoStock = repuestosBajoStock,
            PromedioHorasHastaInicio = CalcularPromedio(horasHastaInicio),
            PromedioHorasHastaCierre = CalcularPromedio(horasHastaCierre),
            OrdenesAltaPrioridadPendientes = ordenesAltaPrioridadPendientes,
            OrdenesPendientesDeValidacion = ordenesPendientesDeValidacion,
            OrdenesRecientes = ordenesRecientes
        });
    }

    [Authorize(Policy = Politicas.ReporteConsultar)]
    [HttpGet("{tipoReporte}/pdf")]
    public IActionResult DescargarReportePdf(string tipoReporte, [FromQuery] FiltrosIndicadoresDto filtros)
    {
        if (filtros.FechaDesde is not null && filtros.FechaHasta is not null
            && filtros.FechaDesde > filtros.FechaHasta)
        {
            return BadRequest(new
            {
                codigo = "RANGO_FECHAS_INVALIDO",
                mensaje = "La fecha desde no puede ser posterior a la fecha hasta."
            });
        }

        var todasLasOrdenes = ListarTodasLasOrdenes();
        var ordenes = new List<OrdenTrabajoDto>();
        foreach (var orden in todasLasOrdenes)
        {
            if (CumpleFiltros(orden, filtros))
            {
                ordenes.Add(orden);
            }
        }

        var criterios = ArmarCriterios(filtros, todasLasOrdenes);
        var tablas = new List<TablaReporte>();
        string titulo;

        if (tipoReporte == "ordenes-trabajo")
        {
            titulo = "Reporte de órdenes de trabajo";
            tablas.Add(ArmarTablaOrdenes(ordenes));
        }
        else if (tipoReporte == "activos-intervenidos")
        {
            titulo = "Reporte de activos intervenidos";
            tablas.Add(ArmarTablaActivos(ordenes));
        }
        else if (tipoReporte == "repuestos-utilizados")
        {
            titulo = "Reporte de repuestos utilizados";
            tablas.Add(ArmarTablaRepuestos(ordenes));
        }
        else if (tipoReporte == "actividad-tecnicos")
        {
            titulo = "Reporte de actividad por técnico";
            tablas.Add(ArmarTablaTecnicos(ordenes));
        }
        else if (tipoReporte == "indicadores")
        {
            titulo = "Reporte de indicadores operativos";
            tablas.Add(ArmarTablaResumen(ordenes));
            tablas.Add(ArmarTablaGrupos("Órdenes por estado", "Estado", ordenes, "estado"));
            tablas.Add(ArmarTablaGrupos("Órdenes por prioridad", "Prioridad", ordenes, "prioridad"));
            tablas.Add(ArmarTablaGrupos("Órdenes por tipo de mantenimiento", "Tipo", ordenes, "tipo"));
            tablas.Add(ArmarTablaTecnicos(ordenes));
            tablas.Add(ArmarTablaActivos(ordenes));
        }
        else
        {
            return BadRequest(new
            {
                codigo = "TIPO_REPORTE_INVALIDO",
                mensaje = "El tipo de reporte no es válido."
            });
        }

        var archivo = GeneradorReportesPdf.Generar(titulo, criterios, tablas);
        return File(archivo, "application/pdf", "reporte-" + tipoReporte + ".pdf");
    }

    private List<OrdenTrabajoDto> ListarTodasLasOrdenes()
    {
        var ordenes = new List<OrdenTrabajoDto>();
        foreach (var ordenTrabajo in _ordenTrabajoDao.ListarTodo())
        {
            ordenes.Add(OrdenTrabajoMapeador.ADto(ordenTrabajo));
        }

        return ordenes;
    }

    private List<OrdenTrabajoDto> ListarOrdenesFiltradas(FiltrosIndicadoresDto filtros)
    {
        var ordenes = new List<OrdenTrabajoDto>();
        foreach (var orden in ListarTodasLasOrdenes())
        {
            if (CumpleFiltros(orden, filtros))
            {
                ordenes.Add(orden);
            }
        }

        return ordenes;
    }

    private static List<string> ArmarCriterios(FiltrosIndicadoresDto filtros, List<OrdenTrabajoDto> todasLasOrdenes)
    {
        var criterios = new List<string>();

        if (filtros.FechaDesde is not null)
        {
            criterios.Add("Desde: " + filtros.FechaDesde.Value.ToString("dd/MM/yyyy"));
        }

        if (filtros.FechaHasta is not null)
        {
            criterios.Add("Hasta: " + filtros.FechaHasta.Value.ToString("dd/MM/yyyy"));
        }

        if (filtros.ClienteId is not null)
        {
            var nombreCliente = "N° " + filtros.ClienteId;
            foreach (var orden in todasLasOrdenes)
            {
                if (orden.ClienteId == filtros.ClienteId)
                {
                    nombreCliente = orden.NombreCliente;
                }
            }

            criterios.Add("Cliente: " + nombreCliente);
        }

        if (filtros.ActivoId is not null)
        {
            var nombreActivo = "N° " + filtros.ActivoId;
            foreach (var orden in todasLasOrdenes)
            {
                if (orden.ActivoId == filtros.ActivoId)
                {
                    nombreActivo = orden.NombreActivo;
                }
            }

            criterios.Add("Activo: " + nombreActivo);
        }

        if (filtros.TecnicoId is not null)
        {
            var nombreTecnico = "N° " + filtros.TecnicoId;
            foreach (var orden in todasLasOrdenes)
            {
                if (orden.TecnicoId == filtros.TecnicoId && orden.NombreTecnico is not null)
                {
                    nombreTecnico = orden.NombreTecnico;
                }
            }

            criterios.Add("Técnico: " + nombreTecnico);
        }

        if (!string.IsNullOrEmpty(filtros.Estado))
        {
            criterios.Add("Estado: " + filtros.Estado);
        }

        if (!string.IsNullOrEmpty(filtros.Prioridad))
        {
            criterios.Add("Prioridad: " + filtros.Prioridad);
        }

        if (!string.IsNullOrEmpty(filtros.Tipo))
        {
            criterios.Add("Tipo de mantenimiento: " + filtros.Tipo);
        }

        if (criterios.Count == 0)
        {
            criterios.Add("Sin filtros: incluye todas las órdenes de trabajo.");
        }

        return criterios;
    }

    private static TablaReporte ArmarTablaOrdenes(List<OrdenTrabajoDto> ordenes)
    {
        var tabla = new TablaReporte
        {
            Titulo = "Órdenes de trabajo (" + ordenes.Count + ")",
            Columnas = new List<string> { "N°", "Creada", "Cliente", "Activo", "Técnico", "Tipo", "Prioridad", "Estado" }
        };

        foreach (var orden in ordenes)
        {
            var nombreTecnico = "Sin asignar";
            if (orden.NombreTecnico is not null)
            {
                nombreTecnico = orden.NombreTecnico;
            }

            tabla.Filas.Add(new List<string>
            {
                orden.Id.ToString(),
                HoraUruguay.Convertir(orden.CreadoEn).ToString("dd/MM/yyyy"),
                orden.NombreCliente,
                orden.NombreActivo,
                nombreTecnico,
                orden.Tipo,
                orden.Prioridad,
                orden.Estado
            });
        }

        return tabla;
    }

    private static TablaReporte ArmarTablaActivos(List<OrdenTrabajoDto> ordenes)
    {
        var activos = new List<CantidadPorGrupoDto>();
        var clientesDeCadaActivo = new Dictionary<string, string>();
        foreach (var orden in ordenes)
        {
            SumarUno(activos, orden.NombreActivo);
            clientesDeCadaActivo[orden.NombreActivo] = orden.NombreCliente;
        }

        var tabla = new TablaReporte
        {
            Titulo = "Activos intervenidos",
            Columnas = new List<string> { "Activo", "Cliente", "Órdenes de trabajo" }
        };

        foreach (var activo in activos.OrderByDescending(grupo => grupo.Cantidad))
        {
            tabla.Filas.Add(new List<string>
            {
                activo.Nombre,
                clientesDeCadaActivo[activo.Nombre],
                activo.Cantidad.ToString()
            });
        }

        return tabla;
    }

    private static TablaReporte ArmarTablaRepuestos(List<OrdenTrabajoDto> ordenes)
    {
        var repuestos = new List<CantidadPorGrupoDto>();
        foreach (var orden in ordenes)
        {
            foreach (var repuesto in orden.Repuestos)
            {
                SumarUno(repuestos, repuesto.Codigo + " - " + repuesto.Nombre);
            }
        }

        var tabla = new TablaReporte
        {
            Titulo = "Repuestos utilizados",
            Columnas = new List<string> { "Repuesto", "Órdenes de trabajo en las que se usó" }
        };

        foreach (var repuesto in repuestos.OrderByDescending(grupo => grupo.Cantidad))
        {
            tabla.Filas.Add(new List<string> { repuesto.Nombre, repuesto.Cantidad.ToString() });
        }

        return tabla;
    }

    private static TablaReporte ArmarTablaTecnicos(List<OrdenTrabajoDto> ordenes)
    {
        var asignadas = new List<CantidadPorGrupoDto>();
        var terminadas = new List<CantidadPorGrupoDto>();
        foreach (var orden in ordenes)
        {
            if (orden.NombreTecnico is null)
            {
                continue;
            }

            SumarUno(asignadas, orden.NombreTecnico);
            if (orden.Estado == "finalizada" || orden.Estado == "cerrada")
            {
                SumarUno(terminadas, orden.NombreTecnico);
            }
        }

        var tabla = new TablaReporte
        {
            Titulo = "Actividad por técnico",
            Columnas = new List<string> { "Técnico", "Órdenes asignadas", "Finalizadas o cerradas", "Pendientes" }
        };

        foreach (var tecnico in asignadas)
        {
            var cantidadTerminadas = 0;
            foreach (var grupo in terminadas)
            {
                if (grupo.Nombre == tecnico.Nombre)
                {
                    cantidadTerminadas = grupo.Cantidad;
                }
            }

            var cantidadPendientes = tecnico.Cantidad - cantidadTerminadas;
            tabla.Filas.Add(new List<string>
            {
                tecnico.Nombre,
                tecnico.Cantidad.ToString(),
                cantidadTerminadas.ToString(),
                cantidadPendientes.ToString()
            });
        }

        return tabla;
    }

    private static TablaReporte ArmarTablaResumen(List<OrdenTrabajoDto> ordenes)
    {
        var horasHastaInicio = new List<double>();
        var horasHastaCierre = new List<double>();
        foreach (var orden in ordenes)
        {
            if (orden.FechaInicio is not null)
            {
                horasHastaInicio.Add((orden.FechaInicio.Value - orden.CreadoEn).TotalHours);
            }

            if (orden.FechaCierre is not null)
            {
                horasHastaCierre.Add((orden.FechaCierre.Value - orden.CreadoEn).TotalHours);
            }
        }

        var tabla = new TablaReporte
        {
            Titulo = "Resumen",
            Columnas = new List<string> { "Indicador", "Valor" }
        };
        tabla.Filas.Add(new List<string> { "Órdenes de trabajo registradas", ordenes.Count.ToString() });
        tabla.Filas.Add(new List<string> { "Tiempo promedio hasta el inicio", FormatearHoras(CalcularPromedio(horasHastaInicio)) });
        tabla.Filas.Add(new List<string> { "Tiempo promedio hasta el cierre", FormatearHoras(CalcularPromedio(horasHastaCierre)) });
        return tabla;
    }

    private static TablaReporte ArmarTablaGrupos(string titulo, string columna, List<OrdenTrabajoDto> ordenes,
        string campo)
    {
        var grupos = new List<CantidadPorGrupoDto>();
        foreach (var orden in ordenes)
        {
            if (campo == "estado")
            {
                SumarUno(grupos, orden.Estado);
            }
            else if (campo == "prioridad")
            {
                SumarUno(grupos, orden.Prioridad);
            }
            else
            {
                SumarUno(grupos, orden.Tipo);
            }
        }

        var tabla = new TablaReporte
        {
            Titulo = titulo,
            Columnas = new List<string> { columna, "Cantidad" }
        };

        foreach (var grupo in grupos)
        {
            tabla.Filas.Add(new List<string> { grupo.Nombre, grupo.Cantidad.ToString() });
        }

        return tabla;
    }

    private static string FormatearHoras(double? horas)
    {
        if (horas is null)
        {
            return "Sin datos";
        }

        return horas + " h";
    }

    private static double? CalcularPromedio(List<double> valores)
    {
        if (valores.Count == 0)
        {
            return null;
        }

        return Math.Round(valores.Average(), 1);
    }

    private static bool CumpleFiltros(OrdenTrabajoDto orden, FiltrosIndicadoresDto filtros)
    {
        var fechaCreacion = DateOnly.FromDateTime(orden.CreadoEn);

        if (filtros.FechaDesde is not null && fechaCreacion < filtros.FechaDesde)
        {
            return false;
        }

        if (filtros.FechaHasta is not null && fechaCreacion > filtros.FechaHasta)
        {
            return false;
        }

        if (filtros.TecnicoId is not null && orden.TecnicoId != filtros.TecnicoId)
        {
            return false;
        }

        if (filtros.ClienteId is not null && orden.ClienteId != filtros.ClienteId)
        {
            return false;
        }

        if (filtros.ActivoId is not null && orden.ActivoId != filtros.ActivoId)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filtros.Estado) && orden.Estado != filtros.Estado)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filtros.Tipo) && orden.Tipo != filtros.Tipo)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(filtros.Prioridad) && orden.Prioridad != filtros.Prioridad)
        {
            return false;
        }

        return true;
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
