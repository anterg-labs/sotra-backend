using System.Security.Cryptography;
using System.Text.Json;
using Api.DTOs.OrdenesTrabajo;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Mapeadores;

public static class OrdenTrabajoMapeador
{
    private const short PrioridadBajaEnBase = 1;
    private const short PrioridadMediaEnBase = 3;
    private const short PrioridadAltaEnBase = 5;
    private const string FormatoDeFecha = "yyyy-MM-dd";

    /// <param name="soloTecnico">Quien pide es solo técnico: ve únicamente las transiciones que puede hacer.</param>
    public static OrdenTrabajoDto ADto(OrdenTrabajo orden, ReglasDeEstadoOrden reglas, bool soloTecnico = false)
    {
        var estado = reglas.Inicial.Nombre;
        var fechaPrevista = orden.FechaPrevistaEjecucion.ToString(FormatoDeFecha);

        var ultimoEstado = orden.HistorialEstadoOts
            .OrderByDescending(historial => historial.FechaHora)
            .ThenByDescending(historial => historial.IdHEstadoOt)
            .FirstOrDefault();

        if (ultimoEstado is not null)
        {
            estado = ultimoEstado.IdEstadoOtNavigation.Nombre;
        }

        var primerEstado = orden.HistorialEstadoOts
            .OrderBy(historial => historial.FechaHora)
            .ThenBy(historial => historial.IdHEstadoOt)
            .FirstOrDefault();

        var nombreActivo = orden.IdActivoNavigation.Nombre;
        if (string.IsNullOrWhiteSpace(nombreActivo))
        {
            nombreActivo = orden.IdActivoNavigation.NumSerie;
        }

        string? nombreTecnico = null;
        if (orden.IdUsuarioTecnicoNavigation is not null)
        {
            nombreTecnico = orden.IdUsuarioTecnicoNavigation.Nombre + " " +
                orden.IdUsuarioTecnicoNavigation.Apellido;
        }

        var tipo = "correctivo";
        if (orden.Tipo)
        {
            tipo = "preventivo";
        }

        var observaciones = "";
        if (orden.Resultado is not null)
        {
            observaciones = orden.Resultado;
        }

        var sugerencias = new List<string>();
        foreach (var recomendacion in orden.HistorialRecomendacionesIa.OrderBy(item => item.IdRecomendacionIa))
        {
            if (recomendacion.Estado)
            {
                sugerencias.Add(recomendacion.Recomendacion);
            }
        }

        var historialEstados = new List<HistorialEstadoDto>();
        var historialOrdenado = orden.HistorialEstadoOts
            .OrderBy(historial => historial.FechaHora)
            .ThenBy(historial => historial.IdHEstadoOt);
        foreach (var historial in historialOrdenado)
        {
            historialEstados.Add(new HistorialEstadoDto
            {
                Estado = historial.IdEstadoOtNavigation.Nombre,
                EstadoAnterior = historial.IdEstadoAnteriorNavigation?.Nombre,
                FechaHora = historial.FechaHora,
                Observacion = historial.Observacion,
                UsuarioId = historial.IdUsuario,
                NombreUsuario = NombreCompleto(historial.IdUsuarioNavigation)
            });
        }

        var historialTecnicos = orden.HistorialTecnicoOts
            .OrderBy(historial => historial.FechaHora)
            .ThenBy(historial => historial.IdHTecnicoOt)
            .Select(historial => new HistorialTecnicoDto
            {
                TecnicoAnteriorId = historial.IdTecnicoAnterior,
                NombreTecnicoAnterior = NombreCompleto(historial.IdTecnicoAnteriorNavigation),
                TecnicoId = historial.IdTecnicoNuevo,
                NombreTecnico = NombreCompleto(historial.IdTecnicoNuevoNavigation),
                FechaHora = historial.FechaHora,
                UsuarioId = historial.IdUsuario,
                NombreUsuario = NombreCompleto(historial.IdUsuarioNavigation),
                Observacion = historial.Observacion
            })
            .ToList();

        var repuestos = new List<RepuestoOrdenDto>();
        foreach (var relacion in orden.OtRepuestos.Where(item => item.Estado != EstadosRepuestoOrden.Liberado).OrderBy(item => item.IdOtRepuesto))
        {
            repuestos.Add(new RepuestoOrdenDto
            {
                Id = relacion.IdRepuestoNavigation.IdRepuesto,
                Codigo = relacion.IdRepuestoNavigation.Codigo,
                Nombre = relacion.IdRepuestoNavigation.Nombre,
                Cantidad = relacion.Cantidad,
                Estado = relacion.Estado
            });
        }

        var historialRepuestos = orden.HistorialRepuestoOts
            .OrderBy(item => item.FechaHora)
            .ThenBy(item => item.IdHRepuestoOt)
            .Select(item => new HistorialRepuestoDto
            {
                RepuestoId = item.IdRepuesto,
                Codigo = item.IdRepuestoNavigation.Codigo,
                Nombre = item.IdRepuestoNavigation.Nombre,
                Accion = item.Accion,
                CantidadAnterior = item.CantidadAnterior,
                CantidadNueva = item.CantidadNueva,
                EstadoAnterior = item.EstadoAnterior,
                EstadoNuevo = item.EstadoNuevo,
                FechaHora = item.FechaHora,
                UsuarioId = item.IdUsuario,
                NombreUsuario = NombreCompleto(item.IdUsuarioNavigation)
            })
            .ToList();

        var dto = new OrdenTrabajoDto
        {
            Id = orden.IdOrden,
            ClienteId = orden.IdCliente,
            NombreCliente = orden.IdClienteNavigation.RazonSocial,
            ActivoId = orden.IdActivo,
            NombreActivo = nombreActivo,
            ResponsableId = orden.IdUsuarioResponsable,
            NombreResponsable = NombreCompleto(orden.IdUsuarioResponsableNavigation),
            TecnicoId = orden.IdUsuarioTecnico,
            NombreTecnico = nombreTecnico,
            Tipo = tipo,
            Prioridad = APrioridadTexto(orden.Prioridad),
            Descripcion = orden.Descripcion,
            Observaciones = observaciones,
            FechaPrevistaEjecucion = fechaPrevista,
            TiempoEstimadoHoras = orden.TiempoEstimadoHoras,
            CausaFallaId = orden.IdCausaFalla,
            CausaFalla = orden.IdCausaFallaNavigation?.Nombre,
            LecturaUso = orden.LecturaUso,
            UnidadUso = orden.IdActivoNavigation.UnidadUso,
            HistorialLecturaUso = orden.HistorialLecturaUsoOts
                .OrderBy(item => item.FechaHora).ThenBy(item => item.IdHLecturaUsoOt)
                .Select(item => new HistorialLecturaUsoDto
                {
                    LecturaAnterior = item.LecturaAnterior,
                    LecturaNueva = item.LecturaNueva,
                    FechaHora = item.FechaHora,
                    UsuarioId = item.IdUsuario,
                    NombreUsuario = NombreCompleto(item.IdUsuarioNavigation)
                })
                .ToList(),
            Estado = estado,
            CreadoEn = orden.FechaCreada,
            FechaInicio = orden.FechaInicio,
            FechaCierre = orden.FechaCierre,
            Sugerencias = sugerencias,
            Repuestos = repuestos,
            Transiciones = reglas.TransicionesDesde(estado)
                .Where(t => !soloTecnico || t.PuedeTecnico)
                .Select(t => new TransicionOrdenDto(t.Destino, t.ExigeMotivo, t.PuedeTecnico, t.Accion, t.Principal))
                .ToList(),
            HistorialEstados = historialEstados,
            HistorialTecnicos = historialTecnicos,
            HistorialRepuestos = historialRepuestos
        };
        dto.Version = CalcularVersion(dto);
        return dto;
    }

    private static string? NombreCompleto(Usuario? usuario) =>
        usuario is null ? null : usuario.Nombre + " " + usuario.Apellido;

    private static string CalcularVersion(OrdenTrabajoDto dto)
    {
        var huella = SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(dto));
        return Convert.ToHexStringLower(huella.AsSpan(0, 8));
    }

    public static bool ATipoBooleano(string tipo)
    {
        if (tipo == "preventivo")
        {
            return true;
        }

        if (tipo == "correctivo")
        {
            return false;
        }

        throw new ArgumentException("El tipo debe ser 'preventivo' o 'correctivo'.");
    }

    public static short APrioridadNumero(string prioridad)
    {
        if (prioridad == "baja")
        {
            return PrioridadBajaEnBase;
        }

        if (prioridad == "media")
        {
            return PrioridadMediaEnBase;
        }

        if (prioridad == "alta")
        {
            return PrioridadAltaEnBase;
        }

        throw new ArgumentException("La prioridad debe ser 'baja', 'media' o 'alta'.");
    }

    public static string APrioridadTexto(short prioridad)
    {
        if (prioridad < PrioridadMediaEnBase)
        {
            return "baja";
        }

        if (prioridad < PrioridadAltaEnBase)
        {
            return "media";
        }

        return "alta";
    }
}
