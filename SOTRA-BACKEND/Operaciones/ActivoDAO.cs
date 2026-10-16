using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class ActivoDAO
{
    private readonly SotraContext _contexto;

    public ActivoDAO() : this(new SotraContext())
    {
    }

    public ActivoDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }


    public List<Activo> ListarTodo()
    {
        return _contexto.Activos
            .OrderBy(activo => activo.IdActivo)
            .ToList();
    }

    public (List<Activo> Elementos, int Total) ListarPagina(FiltroActivos filtro, int pagina, int tamanoPagina)
    {
        var consulta = _contexto.Activos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = ConsultasDeTexto.PatronContiene(filtro.Buscar);
            consulta = consulta.Where(a => EF.Functions.ILike(a.NumSerie, patron)
                                           || (a.Nombre != null && EF.Functions.ILike(a.Nombre, patron))
                                           || (a.Ubicacion != null && EF.Functions.ILike(a.Ubicacion, patron))
                                           || (a.Descripcion != null && EF.Functions.ILike(a.Descripcion, patron)));
        }

        if (filtro.ClienteId is { } clienteId)
        {
            consulta = consulta.Where(a => a.IdCliente == clienteId);
        }

        if (filtro.ModeloMaquinaId is { } modeloMaquinaId)
        {
            consulta = consulta.Where(a => a.IdModeloMaquina == modeloMaquinaId);
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(a => a.Estado == estado);
        }

        return ConsultasDeTexto.Paginar(consulta.OrderBy(a => a.IdActivo), pagina, tamanoPagina);
    }

    public Activo? SeleccionarActivo(long id)
    {
        return _contexto.Activos
            .FirstOrDefault(activo => activo.IdActivo == id);
    }

    public Activo InsertarActivo(Activo activo)
    {
        ValidarClienteActivo(activo.IdCliente);
        ValidarModeloMaquinaActivo(activo.IdModeloMaquina);
        _contexto.Activos.Add(activo);
        _contexto.SaveChanges();
        return activo;
    }

    public bool ActualizarActivo(Activo activo)
    {
        var activoExistente = _contexto.Activos
            .FirstOrDefault(a => a.IdActivo == activo.IdActivo);
        if (activoExistente is null) return false;

        ValidarClienteActivo(activo.IdCliente);
        ValidarModeloMaquinaActivo(activo.IdModeloMaquina);

        activoExistente.IdCliente = activo.IdCliente;
        activoExistente.NumSerie = activo.NumSerie;
        activoExistente.Descripcion = activo.Descripcion;
        activoExistente.IdModeloMaquina = activo.IdModeloMaquina;
        activoExistente.AnioFabricacion = activo.AnioFabricacion;
        activoExistente.Nombre = activo.Nombre;
        activoExistente.Ubicacion = activo.Ubicacion;
        activoExistente.Estado = activo.Estado;

        _contexto.SaveChanges();
        return true;
    }

    public bool DarBajaLogicaActivo(long id)
    {
        var activoExistente = _contexto.Activos
            .FirstOrDefault(activo => activo.IdActivo == id);
        if (activoExistente is null) return false;

        if (!activoExistente.Estado) return true;

        activoExistente.Estado = false;
        _contexto.SaveChanges();
        return true;
    }

    public bool EliminarActivo(long id)
    {
        var activoExistente = _contexto.Activos
            .FirstOrDefault(activo => activo.IdActivo == id);
        if (activoExistente is null) return false;

        _contexto.Activos.Remove(activoExistente);
        _contexto.SaveChanges();
        return true;
    }

    private void ValidarClienteActivo(long idCliente)
    {
        if (idCliente <= 0)
            throw new ArgumentException("El identificador del cliente debe ser positivo.");

        var cliente = _contexto.Clientes.AsNoTracking()
            .FirstOrDefault(cliente => cliente.IdCliente == idCliente);
        if (cliente is null)
            throw new ArgumentException("El cliente indicado no existe.");
        if (!cliente.Estado)
            throw new ArgumentException("El cliente indicado está inactivo.");
    }

    private void ValidarModeloMaquinaActivo(long idModeloMaquina)
    {
        if (idModeloMaquina <= 0)
            throw new ArgumentException("El identificador del modelo de máquina debe ser positivo.");

        var modelo = _contexto.ModeloMaquinas.AsNoTracking()
            .FirstOrDefault(modelo => modelo.IdModeloMaquina == idModeloMaquina);
        if (modelo is null)
            throw new ArgumentException("El modelo de máquina indicado no existe.");
        if (!modelo.Activo)
            throw new ArgumentException("El modelo de máquina indicado está inactivo.");
    }
}
