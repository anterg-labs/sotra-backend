using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using SOTRA_BACKEND.Seguridad;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones
{
    public class UsuarioDAO
    {
        private readonly SotraContext contexto;

        public UsuarioDAO() : this(new SotraContext())
        {
        }

        public UsuarioDAO(SotraContext contexto)
        {
            this.contexto = contexto;
        }

        public Task<List<T>> ListarTodoAsync<T>(Expression<Func<Usuario, T>> proyeccion,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(proyeccion);
            return contexto.Usuarios.AsNoTracking().OrderBy(u => u.IdUsuario)
                .Select(proyeccion).ToListAsync(cancellationToken);
        }

        public List<Usuario> listarTodo()
        {
            return contexto.Usuarios
                .Include(u => u.IdRols)
                .ToList();
        }

        public Usuario? seleccionarUsuario(long id)
        {
            return contexto.Usuarios
                .Include(u => u.IdRols)
                .FirstOrDefault(u => u.IdUsuario == id);
        }

        public Usuario insertarUsuario(Usuario usuario, IEnumerable<long>? idsRol)
        {
            var idsRolNormalizados = NormalizarIdsRol(idsRol);

            var rolesValidos = ObtenerRolesValidos(idsRolNormalizados);
            var hash = Contrasenas.CrearHash(usuario.Contrasena);
            usuario.IdRols = rolesValidos;
            usuario.Contrasena = hash;
            contexto.Usuarios.Add(usuario);
            contexto.SaveChanges();
            return usuario;
        }


        public bool actualizarUsuario(Usuario usuario, IEnumerable<long>? idsRol, string? nuevaContrasena = null)
        {
            var idsRolNormalizados = NormalizarIdsRol(idsRol);
            var usuarioExistente = contexto.Usuarios.Include(u => u.IdRols)
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
            contexto.SaveChanges();
            return true;
        }

        public bool darBajaLogicaUsuario(long id)
        {
            var usuarioExistente = contexto.Usuarios
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
            contexto.SaveChanges();
            return true;
        }

        public bool eliminarUsuario(long id)
        {
            var usuarioExistente = contexto.Usuarios
                .Include(u => u.IdRols)
                .FirstOrDefault(u => u.IdUsuario == id);

            if (usuarioExistente is null)
            {
                return false;
            }

            usuarioExistente.IdRols.Clear();
            contexto.Usuarios.Remove(usuarioExistente);
            contexto.SaveChanges();
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
                throw new ArgumentException("Los ids de rol deben ser positivos.", nameof(idsRol));
            return ids.Distinct().ToList();
        }

        private List<Rol> ObtenerRolesValidos(IReadOnlyCollection<long> idsRol)
        {
            if (idsRol.Count == 0)
            {
                return new List<Rol>();
            }

            var rolesEncontrados = contexto.Rols
                .Where(r => idsRol.Contains(r.IdRol))
                .ToList();

            var idsEncontrados = rolesEncontrados.Select(r => r.IdRol).ToHashSet();
            var idsInexistentes = idsRol.Where(id => !idsEncontrados.Contains(id)).ToList();
            if (idsInexistentes.Count > 0)
            {
                throw new ArgumentException($"Los siguientes roles no existen: {string.Join(", ", idsInexistentes)}", nameof(idsRol));
            }

            var idsInactivos = rolesEncontrados.Where(r => !r.Activo).Select(r => r.IdRol).ToList();
            if (idsInactivos.Count > 0)
            {
                throw new ArgumentException($"Los siguientes roles están inactivos: {string.Join(", ", idsInactivos)}", nameof(idsRol));
            }

            return rolesEncontrados;
        }

    }
}
