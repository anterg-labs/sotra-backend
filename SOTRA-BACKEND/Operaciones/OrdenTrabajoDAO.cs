using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class OrdenTrabajoDAO
{
    private const string FormatoDeFecha = "yyyy-MM-dd";

    private static readonly string[] EstadosValidos =
    {
        "creada", "asignada", "en_ejecucion", "pausada", "finalizada", "cerrada", "cancelada"
    };

    private readonly SotraContext _contexto;

    public OrdenTrabajoDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }

    public List<OrdenTrabajo> ListarTodo()
    {
        return ConsultaCompleta().AsNoTracking().OrderBy(orden => orden.IdOrden).ToList();
    }

    public OrdenTrabajo? Seleccionar(long id)
    {
        return ConsultaCompleta().FirstOrDefault(orden => orden.IdOrden == id);
    }

    public OrdenTrabajo Crear(OrdenTrabajo orden, DateOnly fechaPrevista, List<long> repuestosIds,
        List<string> sugerencias)
    {
        ValidarRelaciones(orden.IdCliente, orden.IdActivo, orden.IdUsuarioTecnico, repuestosIds);
        orden.IdUsuarioResponsable = ObtenerResponsable();
        orden.FechaCreada = DateTime.UtcNow;

        using var transaccion = _contexto.Database.BeginTransaction();
        _contexto.OrdenTrabajos.Add(orden);
        _contexto.SaveChanges();

        AgregarEstado(orden, "creada", fechaPrevista.ToString(FormatoDeFecha));
        ReemplazarRepuestos(orden, repuestosIds);
        ReemplazarSugerencias(orden, sugerencias);
        _contexto.SaveChanges();
        transaccion.Commit();

        return Seleccionar(orden.IdOrden)!;
    }

    public OrdenTrabajo Actualizar(OrdenTrabajo orden, DateOnly fechaPrevista, List<long> repuestosIds,
        List<string> sugerencias, string estado)
    {
        var estadoActual = ObtenerEstadoActual(orden);
        if (estadoActual == "cerrada")
        {
            throw new InvalidOperationException("ORDEN_CERRADA");
        }

        ValidarEstado(estado);
        ValidarRelaciones(orden.IdCliente, orden.IdActivo, orden.IdUsuarioTecnico, repuestosIds);

        using var transaccion = _contexto.Database.BeginTransaction();

        var primerEstado = orden.HistorialEstadoOts.OrderBy(item => item.FechaHora).FirstOrDefault();
        if (primerEstado is not null)
        {
            primerEstado.Observacion = fechaPrevista.ToString(FormatoDeFecha);
        }

        if (estado != estadoActual)
        {
            AgregarEstado(orden, estado, null);
            if (estado == "en_ejecucion" && orden.FechaInicio is null)
            {
                orden.FechaInicio = DateTime.UtcNow;
            }

            if (estado == "cerrada")
            {
                orden.FechaCierre = DateTime.UtcNow;
            }
        }

        ReemplazarRepuestos(orden, repuestosIds);
        ReemplazarSugerencias(orden, sugerencias);
        _contexto.SaveChanges();
        transaccion.Commit();

        return Seleccionar(orden.IdOrden)!;
    }

    private IQueryable<OrdenTrabajo> ConsultaCompleta()
    {
        return _contexto.OrdenTrabajos
            .Include(orden => orden.IdClienteNavigation)
            .Include(orden => orden.IdActivoNavigation)
            .Include(orden => orden.IdUsuarioTecnicoNavigation)
            .Include(orden => orden.HistorialEstadoOts)
                .ThenInclude(historial => historial.IdEstadoOtNavigation)
            .Include(orden => orden.HistorialRecomendacionesIa)
            .Include(orden => orden.OtRepuestos)
                .ThenInclude(relacion => relacion.IdRepuestoNavigation);
    }

    private void ValidarRelaciones(long clienteId, long activoId, long? tecnicoId, List<long> repuestosIds)
    {
        var cliente = _contexto.Clientes.AsNoTracking().FirstOrDefault(item => item.IdCliente == clienteId);
        if (cliente is null || !cliente.Estado)
        {
            throw new ArgumentException("El cliente indicado no existe o está inactivo.");
        }

        var activo = _contexto.Activos.AsNoTracking().FirstOrDefault(item => item.IdActivo == activoId);
        if (activo is null || !activo.Estado || activo.IdCliente != clienteId)
        {
            throw new ArgumentException("El activo indicado no pertenece al cliente o está inactivo.");
        }

        if (tecnicoId.HasValue)
        {
            var tecnico = _contexto.Usuarios.AsNoTracking()
                .Include(item => item.IdRols)
                .FirstOrDefault(item => item.IdUsuario == tecnicoId.Value);
            if (tecnico is null || !tecnico.Estado)
            {
                throw new ArgumentException("El técnico indicado no existe o está inactivo.");
            }

            var tieneRolTecnico = tecnico.IdRols.Any(rol => rol.Nombre == "Técnico" && rol.Activo);
            if (!tieneRolTecnico)
            {
                throw new ArgumentException("El usuario indicado no tiene el rol Técnico activo.");
            }
        }

        foreach (var repuestoId in repuestosIds.Distinct())
        {
            var repuesto = _contexto.Repuestos.AsNoTracking()
                .FirstOrDefault(item => item.IdRepuesto == repuestoId);
            if (repuesto is null || !repuesto.Estado)
            {
                throw new ArgumentException("Uno de los repuestos no existe o está inactivo.");
            }
        }
    }

    private long ObtenerResponsable()
    {
        var jefe = _contexto.Usuarios.AsNoTracking()
            .Where(item => item.Estado)
            .Where(item => item.IdRols.Any(rol => rol.Nombre == "Jefe de mantenimiento" && rol.Activo))
            .OrderBy(item => item.IdUsuario)
            .FirstOrDefault();
        if (jefe is null)
        {
            throw new ArgumentException("No existe un jefe de mantenimiento activo para asignar como responsable.");
        }

        return jefe.IdUsuario;
    }

    private void AgregarEstado(OrdenTrabajo orden, string nombreEstado, string? observacion)
    {
        ValidarEstado(nombreEstado);
        var estado = _contexto.EstadoOts.FirstOrDefault(item => item.Nombre == nombreEstado);
        if (estado is null)
        {
            estado = new EstadoOt { Nombre = nombreEstado, Estado = true };
            _contexto.EstadoOts.Add(estado);
        }

        orden.HistorialEstadoOts.Add(new HistorialEstadoOt
        {
            IdEstadoOtNavigation = estado,
            FechaHora = DateTime.UtcNow,
            Observacion = observacion
        });
    }

    private static void ValidarEstado(string estado)
    {
        if (!EstadosValidos.Contains(estado))
        {
            throw new ArgumentException("El estado indicado no es válido.");
        }
    }

    private static string ObtenerEstadoActual(OrdenTrabajo orden)
    {
        var ultimoEstado = orden.HistorialEstadoOts
            .OrderByDescending(item => item.FechaHora)
            .ThenByDescending(item => item.IdHEstadoOt)
            .FirstOrDefault();
        if (ultimoEstado is null)
        {
            return "creada";
        }

        return ultimoEstado.IdEstadoOtNavigation.Nombre;
    }

    private void ReemplazarRepuestos(OrdenTrabajo orden, List<long> repuestosIds)
    {
        _contexto.OtRepuestos.RemoveRange(orden.OtRepuestos);
        orden.OtRepuestos.Clear();
        foreach (var repuestoId in repuestosIds.Distinct())
        {
            orden.OtRepuestos.Add(new OtRepuesto { IdRepuesto = repuestoId });
        }
    }

    private void ReemplazarSugerencias(OrdenTrabajo orden, List<string> sugerencias)
    {
        foreach (var sugerenciaAnterior in orden.HistorialRecomendacionesIa)
        {
            sugerenciaAnterior.Estado = false;
        }

        foreach (var texto in sugerencias)
        {
            if (!string.IsNullOrWhiteSpace(texto))
            {
                orden.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa
                {
                    Recomendacion = texto.Trim(),
                    Estado = true
                });
            }
        }
    }
}
