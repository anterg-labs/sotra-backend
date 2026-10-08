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

    private static readonly Dictionary<string, string[]> TransicionesPermitidas = new()
    {
        { "creada", new[] { "asignada", "cancelada" } },
        { "asignada", new[] { "en_ejecucion", "cancelada" } },
        { "en_ejecucion", new[] { "pausada", "finalizada" } },
        { "pausada", new[] { "en_ejecucion", "cancelada" } },
        { "finalizada", new[] { "cerrada", "asignada" } },
        { "cerrada", new string[0] },
        { "cancelada", new string[0] }
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
        List<string> sugerencias, string estado, string? motivo)
    {
        var estadoActual = ObtenerEstadoActual(orden);
        if (estadoActual == "cerrada")
        {
            throw new InvalidOperationException("ORDEN_CERRADA");
        }

        ValidarEstado(estado);
        ValidarTransicion(estadoActual, estado, orden.IdUsuarioTecnico, motivo);
        ValidarRelaciones(orden.IdCliente, orden.IdActivo, orden.IdUsuarioTecnico, repuestosIds);

        using var transaccion = _contexto.Database.BeginTransaction();

        var primerEstado = orden.HistorialEstadoOts.OrderBy(item => item.FechaHora).FirstOrDefault();
        if (primerEstado is not null)
        {
            primerEstado.Observacion = fechaPrevista.ToString(FormatoDeFecha);
        }

        if (estado != estadoActual)
        {
            AgregarEstado(orden, estado, motivo);
            AplicarFechasDeEstado(orden, estado);
        }

        ReemplazarRepuestos(orden, repuestosIds);
        ReemplazarSugerencias(orden, sugerencias);
        _contexto.SaveChanges();
        transaccion.Commit();

        return Seleccionar(orden.IdOrden)!;
    }

    public OrdenTrabajo CambiarEstado(OrdenTrabajo orden, string estado, string? motivo)
    {
        var estadoActual = ObtenerEstadoActual(orden);
        if (estadoActual == "cerrada")
        {
            throw new InvalidOperationException("ORDEN_CERRADA");
        }

        ValidarEstado(estado);
        if (estado == estadoActual)
        {
            throw new InvalidOperationException("TRANSICION_INVALIDA");
        }

        ValidarTransicion(estadoActual, estado, orden.IdUsuarioTecnico, motivo);
        AgregarEstado(orden, estado, motivo);
        AplicarFechasDeEstado(orden, estado);
        _contexto.SaveChanges();

        return Seleccionar(orden.IdOrden)!;
    }

    public OrdenTrabajo AsignarTecnico(OrdenTrabajo orden, long tecnicoId)
    {
        var estadoActual = ObtenerEstadoActual(orden);
        if (estadoActual == "cerrada" || estadoActual == "cancelada")
        {
            throw new InvalidOperationException("ORDEN_NO_MODIFICABLE");
        }

        var tecnico = ValidarTecnico(tecnicoId);
        orden.IdUsuarioTecnico = tecnicoId;

        var estadoNuevo = estadoActual;
        if (estadoActual == "creada")
        {
            estadoNuevo = "asignada";
        }

        AgregarEstado(orden, estadoNuevo, "Técnico asignado: " + tecnico.Nombre + " " + tecnico.Apellido);
        _contexto.SaveChanges();

        return Seleccionar(orden.IdOrden)!;
    }

    public static List<string> ObtenerEstadosSiguientes(string estadoActual)
    {
        var estadosSiguientes = new List<string>();
        if (TransicionesPermitidas.ContainsKey(estadoActual))
        {
            estadosSiguientes.AddRange(TransicionesPermitidas[estadoActual]);
        }

        return estadosSiguientes;
    }

    private static void ValidarTransicion(string estadoActual, string estadoNuevo, long? tecnicoId, string? motivo)
    {
        if (estadoNuevo == estadoActual)
        {
            return;
        }

        if (!ObtenerEstadosSiguientes(estadoActual).Contains(estadoNuevo))
        {
            throw new InvalidOperationException("TRANSICION_INVALIDA");
        }

        if ((estadoNuevo == "asignada" || estadoNuevo == "en_ejecucion") && tecnicoId is null)
        {
            throw new InvalidOperationException("TECNICO_OBLIGATORIO");
        }

        var necesitaMotivo = estadoNuevo == "pausada" || estadoNuevo == "cancelada" || estadoNuevo == "cerrada"
                             || (estadoActual == "finalizada" && estadoNuevo == "asignada");
        if (necesitaMotivo && string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("MOTIVO_OBLIGATORIO");
        }
    }

    private static void AplicarFechasDeEstado(OrdenTrabajo orden, string estado)
    {
        if (estado == "en_ejecucion" && orden.FechaInicio is null)
        {
            orden.FechaInicio = DateTime.UtcNow;
        }

        if (estado == "cerrada")
        {
            orden.FechaCierre = DateTime.UtcNow;
        }
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
            ValidarTecnico(tecnicoId.Value);
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

    private Usuario ValidarTecnico(long tecnicoId)
    {
        var tecnico = _contexto.Usuarios.AsNoTracking()
            .Include(item => item.IdRols)
            .FirstOrDefault(item => item.IdUsuario == tecnicoId);
        if (tecnico is null || !tecnico.Estado)
        {
            throw new ArgumentException("El técnico indicado no existe o está inactivo.");
        }

        var tieneRolTecnico = tecnico.IdRols.Any(rol => rol.Nombre == "Técnico" && rol.Activo);
        if (!tieneRolTecnico)
        {
            throw new ArgumentException("El usuario indicado no tiene el rol Técnico activo.");
        }

        return tecnico;
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
        var textosNuevos = new List<string>();
        foreach (var texto in sugerencias)
        {
            if (!string.IsNullOrWhiteSpace(texto))
            {
                textosNuevos.Add(texto.Trim());
            }
        }

        foreach (var sugerenciaAnterior in orden.HistorialRecomendacionesIa)
        {
            if (!sugerenciaAnterior.Estado)
            {
                continue;
            }

            if (textosNuevos.Contains(sugerenciaAnterior.Recomendacion))
            {
                textosNuevos.Remove(sugerenciaAnterior.Recomendacion);
            }
            else
            {
                sugerenciaAnterior.Estado = false;
            }
        }

        foreach (var texto in textosNuevos)
        {
            orden.HistorialRecomendacionesIa.Add(new HistorialRecomendacionIa
            {
                Recomendacion = texto,
                Estado = true
            });
        }
    }
}
