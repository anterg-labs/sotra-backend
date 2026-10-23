using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public class UsuarioDAO
{
    public static readonly ColumnasOrdenables<Usuario> ColumnasOrdenables = new(
        u => u.IdUsuario,
        [
            new("nombre", u => u.Nombre + " " + u.Apellido),
            new("correo", u => u.Mail),
            new("roles", u => u.IdRols.Count),
            new("estado", u => u.Estado),
            new("creadoEn", u => u.FechaIngreso),
        ]);

    private readonly SotraContext _contexto;

    public UsuarioDAO() : this(new SotraContext())
    {
    }

    public UsuarioDAO(SotraContext contexto)
    {
        _contexto = contexto;
    }

    public async Task<List<Usuario>> ListarTodoAsync(CancellationToken cancelacion)
    {
        return await _contexto.Usuarios
            .Include(u => u.IdRols)
            .OrderBy(u => u.IdUsuario)
            .ToListAsync(cancelacion);
    }

    public async Task<(List<Usuario> Elementos, int Total)> ListarPaginaAsync(
        FiltroUsuarios filtro, int pagina, int tamanoPagina, CancellationToken cancelacion)
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

        var ordenada = ColumnasOrdenables.Aplicar(consulta, filtro.OrdenarPor, filtro.Descendente);

        return await ConsultasDeTexto.PaginarAsync(ordenada.Include(u => u.IdRols), pagina, tamanoPagina, cancelacion);
    }

    public async Task<Usuario?> SeleccionarUsuarioAsync(long id, CancellationToken cancelacion)
    {
        return await _contexto.Usuarios
            .Include(u => u.IdRols)
            .FirstOrDefaultAsync(u => u.IdUsuario == id, cancelacion);
    }

    // Permite que el alta, la modificación y la baja confirmen la base solo si Keycloak
    // también aceptó el cambio.
    public async Task<IDbContextTransaction> IniciarTransaccionAsync(CancellationToken cancelacion)
    {
        return await _contexto.Database.BeginTransactionAsync(cancelacion);
    }

    public async Task GuardarIdKeycloakAsync(long idUsuario, Guid idKeycloak, CancellationToken cancelacion)
    {
        var usuario = await _contexto.Usuarios.FirstAsync(u => u.IdUsuario == idUsuario, cancelacion);
        usuario.IdKeycloak = idKeycloak;
        await _contexto.SaveChangesAsync(cancelacion);
    }

    // Busca el usuario de SOTRA vinculado a un usuario de Keycloak. Si todavía no hay
    // ninguno, vincula al usuario sin vincular que tenga el mismo correo.
    public async Task<Usuario?> SeleccionarOVincularPorKeycloakAsync(
        Guid idKeycloak, string? correo, CancellationToken cancelacion)
    {
        var usuario = await _contexto.Usuarios.FirstOrDefaultAsync(u => u.IdKeycloak == idKeycloak, cancelacion);
        if (usuario is not null || string.IsNullOrWhiteSpace(correo))
        {
            return usuario;
        }

        var correoNormalizado = correo.Trim().ToLower();
        usuario = await _contexto.Usuarios
            .FirstOrDefaultAsync(u => u.IdKeycloak == null && u.Mail.ToLower() == correoNormalizado, cancelacion);
        if (usuario is null)
        {
            return null;
        }

        usuario.IdKeycloak = idKeycloak;
        await _contexto.SaveChangesAsync(cancelacion);
        return usuario;
    }

    public async Task<Usuario> InsertarUsuarioAsync(Usuario usuario, IEnumerable<long>? idsRol, CancellationToken cancelacion)
    {
        var idsRolNormalizados = NormalizarIdsRol(idsRol);

        var rolesValidos = await ObtenerRolesValidosAsync(idsRolNormalizados, cancelacion);
        usuario.IdRols = rolesValidos;
        _contexto.Usuarios.Add(usuario);
        await _contexto.SaveChangesAsync(cancelacion);
        return usuario;
    }

    public async Task<bool> ActualizarUsuarioAsync(
        Usuario usuario, IEnumerable<long>? idsRol, CancellationToken cancelacion)
    {
        var idsRolNormalizados = NormalizarIdsRol(idsRol);
        var usuarioExistente = await _contexto.Usuarios.Include(u => u.IdRols)
            .FirstOrDefaultAsync(u => u.IdUsuario == usuario.IdUsuario, cancelacion);
        if (usuarioExistente is null) return false;

        var rolesAAgregar = await ObtenerRolesValidosAsync(
            idsRolNormalizados.Except(usuarioExistente.IdRols.Select(r => r.IdRol)).ToList(), cancelacion);
        usuarioExistente.Nombre = usuario.Nombre;
        usuarioExistente.Apellido = usuario.Apellido;
        usuarioExistente.Mail = usuario.Mail;
        usuarioExistente.Telefono = usuario.Telefono;
        usuarioExistente.Estado = usuario.Estado;
        usuarioExistente.FechaIngreso = usuario.FechaIngreso;

        foreach (var rol in usuarioExistente.IdRols.Where(r => !idsRolNormalizados.Contains(r.IdRol)).ToList())
            usuarioExistente.IdRols.Remove(rol);
        foreach (var rol in rolesAAgregar) usuarioExistente.IdRols.Add(rol);
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> DarBajaLogicaUsuarioAsync(long id, CancellationToken cancelacion)
    {
        var usuarioExistente = await _contexto.Usuarios
            .FirstOrDefaultAsync(u => u.IdUsuario == id, cancelacion);

        if (usuarioExistente is null)
        {
            return false;
        }

        if (!usuarioExistente.Estado)
        {
            return true;
        }

        usuarioExistente.Estado = false;
        await _contexto.SaveChangesAsync(cancelacion);
        return true;
    }

    public async Task<bool> EliminarUsuarioAsync(long id, CancellationToken cancelacion)
    {
        var usuarioExistente = await _contexto.Usuarios
            .Include(u => u.IdRols)
            .FirstOrDefaultAsync(u => u.IdUsuario == id, cancelacion);

        if (usuarioExistente is null)
        {
            return false;
        }

        usuarioExistente.IdRols.Clear();
        _contexto.Usuarios.Remove(usuarioExistente);
        await _contexto.SaveChangesAsync(cancelacion);
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
            throw new ValidacionDeNegocioException("Los ids de rol deben ser positivos.");
        return ids.Distinct().ToList();
    }

    private async Task<List<Rol>> ObtenerRolesValidosAsync(IReadOnlyCollection<long> idsRol, CancellationToken cancelacion)
    {
        if (idsRol.Count == 0)
        {
            return new List<Rol>();
        }

        var rolesEncontrados = await _contexto.Rols
            .Where(r => idsRol.Contains(r.IdRol))
            .ToListAsync(cancelacion);

        var idsEncontrados = rolesEncontrados.Select(r => r.IdRol).ToHashSet();
        var idsInexistentes = idsRol.Where(id => !idsEncontrados.Contains(id)).ToList();
        if (idsInexistentes.Count > 0)
        {
            throw new ValidacionDeNegocioException($"Los siguientes roles no existen: {string.Join(", ", idsInexistentes)}");
        }

        var idsInactivos = rolesEncontrados.Where(r => !r.Activo).Select(r => r.IdRol).ToList();
        if (idsInactivos.Count > 0)
        {
            throw new ValidacionDeNegocioException($"Los siguientes roles estan inactivos: {string.Join(", ", idsInactivos)}");
        }

        return rolesEncontrados;
    }

}
