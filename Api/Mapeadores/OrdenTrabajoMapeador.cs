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

    public static OrdenTrabajoDto ADto(OrdenTrabajo orden)
    {
        var estado = "creada";
        var fechaPrevista = DateOnly.FromDateTime(orden.FechaCreada).ToString(FormatoDeFecha);

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

        if (primerEstado is not null && DateOnly.TryParse(primerEstado.Observacion, out var fechaGuardada))
        {
            fechaPrevista = fechaGuardada.ToString(FormatoDeFecha);
        }

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
            var observacion = historial.Observacion;
            if (historial == primerEstado)
            {
                observacion = null;
            }

            historialEstados.Add(new HistorialEstadoDto
            {
                Estado = historial.IdEstadoOtNavigation.Nombre,
                FechaHora = historial.FechaHora,
                Observacion = observacion
            });
        }

        var repuestos = new List<RepuestoOrdenDto>();
        foreach (var relacion in orden.OtRepuestos.OrderBy(item => item.IdOtRepuesto))
        {
            repuestos.Add(new RepuestoOrdenDto
            {
                Id = relacion.IdRepuestoNavigation.IdRepuesto,
                Codigo = relacion.IdRepuestoNavigation.Codigo,
                Nombre = relacion.IdRepuestoNavigation.Nombre
            });
        }

        var dto = new OrdenTrabajoDto
        {
            Id = orden.IdOrden,
            ClienteId = orden.IdCliente,
            NombreCliente = orden.IdClienteNavigation.RazonSocial,
            ActivoId = orden.IdActivo,
            NombreActivo = nombreActivo,
            TecnicoId = orden.IdUsuarioTecnico,
            NombreTecnico = nombreTecnico,
            Tipo = tipo,
            Prioridad = APrioridadTexto(orden.Prioridad),
            Descripcion = orden.Descripcion,
            Observaciones = observaciones,
            FechaPrevistaEjecucion = fechaPrevista,
            Estado = estado,
            CreadoEn = orden.FechaCreada,
            FechaInicio = orden.FechaInicio,
            FechaCierre = orden.FechaCierre,
            Sugerencias = sugerencias,
            Repuestos = repuestos,
            EstadosSiguientes = OrdenTrabajoDAO.ObtenerEstadosSiguientes(estado),
            HistorialEstados = historialEstados
        };
        dto.Version = CalcularVersion(dto);
        return dto;
    }

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
