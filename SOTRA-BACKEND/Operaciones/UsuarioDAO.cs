using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Seguridad;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class UsuarioDAO
{
    private readonly SotraContext _contexto;

    public UsuarioDAO() : this(new SotraContext())
    {
    }

    public UsuarioDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }

    public List<Usuario> ListarTodo()
    {
        return _contexto.Usuarios
            .Include(u => u.IdRols)
            .OrderBy(u => u.IdUsuario)
            .ToList();
    }

    public (List<Usuario> Elementos, int Total) ListarPagina(FiltroUsuarios filtro, int pagina, int tamanoPagina)
    {
        var consulta = _contexto.Usuarios.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Buscar))
        {
            var patron = ConsultasDeTexto.PatronContiene(filtro.Buscar);
            consulta = consulta.Where(u => EF.Functions.ILike(u.Nombre + " " + u.Apellido, patron)
                                           || EF.Functions.ILike(u.Mail, patron));
        }

        if (filtro.RolId is { } rolId)
        {
            consulta = consulta.Where(u => u.IdRols.Any(rol => rol.IdRol == rolId));
        }

        if (filtro.Estado is { } estado)
        {
            consulta = consulta.Where(u => u.Estado == estado);
        }

        var total = consulta.Count();
        var ordenada = filtro.Descendente
            ? consulta.OrderByDescending(u => u.IdUsuario)
            : consulta.OrderBy(u => u.IdUsuario);

        var elementos = ordenada
            .Skip((pagina - 1) * tamanoPagina)
            .Take(tamanoPagina)
            .Include(u => u.IdRols)
            .ToList();

        return (elementos, total);
    }

    public Usuario? SeleccionarUsuario(long id)
    {
        return _contexto.Usuarios
            .Include(u => u.IdRols)
            .FirstOrDefault(u => u.IdUsuario == id);
    }

    public Usuario InsertarUsuario(Usuario usuario, IEnumerable<long>? idsRol)
    {
        var idsRolNormalizados = NormalizarIdsRol(idsRol);

        var rolesValidos = ObtenerRolesValidos(idsRolNormalizados);
        var hash = Contrasenas.CrearHash(usuario.Contrasena);
        usuario.IdRols = rolesValidos;
        usuario.Contrasena = hash;
        _contexto.Usuarios.Add(usuario);
        _contexto.SaveChanges();
        return usuario;
    }

    public bool ActualizarUsuario(Usuario usuario, IEnumerable<long>? idsRol, string? nuevaContrasena = null)
    {
        var idsRolNormalizados = NormalizarIdsRol(idsRol);
        var usuarioExistente = _contexto.Usuarios.Include(u => u.IdRols)
            .FirstOrDefault(u => u.IdUsuario == usuario.IdUsuario);
        if (usuarioExistente is null) return false;

        var rolesAAgregar = ObtenerRolesValidos(idsRolNormalizados
            .Except(usuarioExistente.IdRols.Select(r => r.IdRol)).ToList());
        var hash = nuevaContrasena is null ? null : Contrasenas.CrearHash(nuevaContrasena);
        usuarioExistente.Nombre = usuario.Nombre;
        usuarioExistente.Apellido = usuario.Apellido;
        usuarioExistente.Mail = usuario.Mail;
        usuarioExistente.Telefono = usuario.Telefono;
        if (hash is not null) usuarioExistente.Contrasena = hash;
        usuarioExistente.Estado = usuario.Estado;
        usuarioExistente.FechaIngreso = usuario.FechaIngreso;

        foreach (var rol in usuarioExistente.IdRols.Where(r => !idsRolNormalizados.Contains(r.IdRol)).ToList())
            usuarioExistente.IdRols.Remove(rol);
        foreach (var rol in rolesAAgregar) usuarioExistente.IdRols.Add(rol);
        _contexto.SaveChanges();
        return true;
    }

    public bool DarBajaLogicaUsuario(long id)
    {
        var usuarioExistente = _contexto.Usuarios
            .FirstOrDefault(u => u.IdUsuario == id);

        if (usuarioExistente is null)
        {
            return false;
        }

        if (!usuarioExistente.Estado)
        {
            return true;
        }

        usuarioExistente.Estado = false;
        _contexto.SaveChanges();
        return true;
    }

    public bool EliminarUsuario(long id)
    {
        var usuarioExistente = _contexto.Usuarios
            .Include(u => u.IdRols)
            .FirstOrDefault(u => u.IdUsuario == id);

        if (usuarioExistente is null)
        {
            return false;
        }

        usuarioExistente.IdRols.Clear();
        _contexto.Usuarios.Remove(usuarioExistente);
        _contexto.SaveChanges();
        return true;
    }

    private static List<long> NormalizarIdsRol(IEnumerable<long>? idsRol)
    {
        if (idsRol is null)
        {
            return new List<long>();
        }

        var ids = idsRol.ToList();
        if (ids.Any(id => id <= 0))
            throw new ArgumentException("Los ids de rol deben ser positivos.");
        return ids.Distinct().ToList();
    }

    private List<Rol> ObtenerRolesValidos(IReadOnlyCollection<long> idsRol)
    {
        if (idsRol.Count == 0)
        {
            return new List<Rol>();
        }

        var rolesEncontrados = _contexto.Rols
            .Where(r => idsRol.Contains(r.IdRol))
            .ToList();

        var idsEncontrados = rolesEncontrados.Select(r => r.IdRol).ToHashSet();
        var idsInexistentes = idsRol.Where(id => !idsEncontrados.Contains(id)).ToList();
        if (idsInexistentes.Count > 0)
        {
            throw new ArgumentException($"Los siguientes roles no existen: {string.Join(", ", idsInexistentes)}");
        }

        var idsInactivos = rolesEncontrados.Where(r => !r.Activo).Select(r => r.IdRol).ToList();
        if (idsInactivos.Count > 0)
        {
            throw new ArgumentException($"Los siguientes roles estan inactivos: {string.Join(", ", idsInactivos)}");
        }

        return rolesEncontrados;
    }

}
