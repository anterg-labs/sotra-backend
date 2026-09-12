using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones
{
    public class UsuarioDAO
    {
        public SotraContext contexto = new SotraContext();

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

            using var transaccion = contexto.Database.BeginTransaction();
            try
            {
                contexto.Usuarios.Add(usuario);
                contexto.SaveChanges();

                if (idsRolNormalizados.Count > 0)
                {
                    var rolesValidos = ObtenerRolesValidos(idsRolNormalizados);
                    foreach (var rol in rolesValidos)
                    {
                        usuario.IdRols.Add(rol);
                    }
                }

                contexto.SaveChanges();
                transaccion.Commit();

                return usuario;
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
        }

        public bool actualizarUsuario(Usuario usuario, IEnumerable<long>? idsRol)
        {
            var idsRolNormalizados = NormalizarIdsRol(idsRol);

            using var transaccion = contexto.Database.BeginTransaction();
            try
            {
                var usuarioExistente = contexto.Usuarios
                    .Include(u => u.IdRols)
                    .FirstOrDefault(u => u.IdUsuario == usuario.IdUsuario);

                if (usuarioExistente is null)
                {
                    transaccion.Rollback();
                    return false;
                }

                usuarioExistente.Nombre = usuario.Nombre;
                usuarioExistente.Apellido = usuario.Apellido;
                usuarioExistente.Mail = usuario.Mail;
                usuarioExistente.Telefono = usuario.Telefono;
                usuarioExistente.Contrasena = usuario.Contrasena;
                usuarioExistente.Estado = usuario.Estado;
                usuarioExistente.FechaIngreso = usuario.FechaIngreso;

                SincronizarRoles(usuarioExistente, idsRolNormalizados);

                contexto.SaveChanges();
                transaccion.Commit();
                return true;
            }
            catch
            {
                transaccion.Rollback();
                throw;
            }
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

            var ids = idsRol
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (ids.Count != idsRol.Count(id => id > 0))
            {
                throw new ArgumentException("Se enviaron ids de rol inválidos.", nameof(idsRol));
            }

            return ids;
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

        private void SincronizarRoles(Usuario usuario, IReadOnlyCollection<long> idsRolFinales)
        {
            var rolesActuales = usuario.IdRols.Select(r => r.IdRol).ToHashSet();

            var idsAEliminar = rolesActuales.Except(idsRolFinales).ToList();
            if (idsAEliminar.Count > 0)
            {
                var relacionesAEliminar = usuario.IdRols.Where(r => idsAEliminar.Contains(r.IdRol)).ToList();
                foreach (var rol in relacionesAEliminar)
                {
                    usuario.IdRols.Remove(rol);
                }
            }

            var idsAAgregar = idsRolFinales.Except(rolesActuales).ToList();
            if (idsAAgregar.Count > 0)
            {
                var rolesAAgregar = ObtenerRolesValidos(idsAAgregar);
                foreach (var rol in rolesAAgregar)
                {
                    usuario.IdRols.Add(rol);
                }
            }
        }
    }
}
