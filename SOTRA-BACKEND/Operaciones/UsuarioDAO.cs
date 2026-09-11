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
                .Include(u => u.Administrador)
                .Include(u => u.Gerente)
                .Include(u => u.JefeMantenimiento)
                .Include(u => u.Panolero)
                .Include(u => u.Tecnico)
                .ToList();
        }

        public Usuario? seleccionarUsuario(long id)
        {
            return contexto.Usuarios
                .Include(u => u.Administrador)
                .Include(u => u.Gerente)
                .Include(u => u.JefeMantenimiento)
                .Include(u => u.Panolero)
                .Include(u => u.Tecnico)
                .FirstOrDefault(u => u.IdUsuario == id);
        }

        public Usuario insertarUsuario(Usuario usuario, string tipoUsuario)
        {
            if (!TryNormalizarTipoUsuario(tipoUsuario, out var tipoRol))
            {
                throw new ArgumentException("Tipo de usuario inválido.", nameof(tipoUsuario));
            }

            contexto.Usuarios.Add(usuario);
            contexto.SaveChanges();

            AsignarRol(usuario.IdUsuario, tipoRol);
            contexto.SaveChanges();

            return usuario;
        }

        public bool actualizarUsuario(Usuario usuario, string tipoUsuario)
        {
            var usuarioExistente = seleccionarUsuario(usuario.IdUsuario);
            if (usuarioExistente is null)
            {
                return false;
            }

            if (!TryNormalizarTipoUsuario(tipoUsuario, out var tipoRol))
            {
                throw new ArgumentException("Tipo de usuario inválido.", nameof(tipoUsuario));
            }

            usuarioExistente.Nombre = usuario.Nombre;
            usuarioExistente.Apellido = usuario.Apellido;
            usuarioExistente.Mail = usuario.Mail;
            usuarioExistente.Telefono = usuario.Telefono;
            usuarioExistente.Contrasena = usuario.Contrasena;
            usuarioExistente.Estado = usuario.Estado;
            usuarioExistente.FechaIngreso = usuario.FechaIngreso;

            LimpiarRoles(usuarioExistente);
            AsignarRol(usuarioExistente.IdUsuario, tipoRol);

            contexto.SaveChanges();
            return true;
        }

        public bool eliminarUsuario(long id)
        {
            var usuarioExistente = seleccionarUsuario(id);
            if (usuarioExistente is null)
            {
                return false;
            }

            LimpiarRoles(usuarioExistente);
            contexto.Usuarios.Remove(usuarioExistente);
            contexto.SaveChanges();
            return true;
        }

        private void LimpiarRoles(Usuario usuario)
        {
            if (usuario.Administrador is not null)
            {
                contexto.Administradors.Remove(usuario.Administrador);
            }

            if (usuario.Gerente is not null)
            {
                contexto.Gerentes.Remove(usuario.Gerente);
            }

            if (usuario.JefeMantenimiento is not null)
            {
                contexto.JefeMantenimientos.Remove(usuario.JefeMantenimiento);
            }

            if (usuario.Panolero is not null)
            {
                contexto.Panoleros.Remove(usuario.Panolero);
            }

            if (usuario.Tecnico is not null)
            {
                contexto.Tecnicos.Remove(usuario.Tecnico);
            }
        }

        private void AsignarRol(long idUsuario, TipoRolUsuario tipoRol)
        {
            switch (tipoRol)
            {
                case TipoRolUsuario.Administrador:
                    contexto.Administradors.Add(new Administrador { IdUsuario = idUsuario });
                    break;
                case TipoRolUsuario.Gerente:
                    contexto.Gerentes.Add(new Gerente { IdUsuario = idUsuario });
                    break;
                case TipoRolUsuario.JefeMantenimiento:
                    contexto.JefeMantenimientos.Add(new JefeMantenimiento { IdUsuario = idUsuario });
                    break;
                case TipoRolUsuario.Panolero:
                    contexto.Panoleros.Add(new Panolero { IdUsuario = idUsuario });
                    break;
                case TipoRolUsuario.Tecnico:
                    contexto.Tecnicos.Add(new Tecnico { IdUsuario = idUsuario });
                    break;
                case TipoRolUsuario.Ninguno:
                default:
                    break;
            }
        }

        private bool TryNormalizarTipoUsuario(string? tipoUsuario, out TipoRolUsuario tipoRol)
        {
            tipoRol = TipoRolUsuario.Ninguno;

            if (string.IsNullOrWhiteSpace(tipoUsuario))
            {
                return true;
            }

            string valor = tipoUsuario.Trim().ToLowerInvariant();

            switch (valor)
            {
                case "0":
                case "ninguno":
                case "sinrol":
                case "sin rol":
                    tipoRol = TipoRolUsuario.Ninguno;
                    return true;
                case "1":
                case "administrador":
                    tipoRol = TipoRolUsuario.Administrador;
                    return true;
                case "2":
                case "gerente":
                    tipoRol = TipoRolUsuario.Gerente;
                    return true;
                case "3":
                case "jefemantenimiento":
                case "jefe mantenimiento":
                    tipoRol = TipoRolUsuario.JefeMantenimiento;
                    return true;
                case "4":
                case "panolero":
                    tipoRol = TipoRolUsuario.Panolero;
                    return true;
                case "5":
                case "tecnico":
                    tipoRol = TipoRolUsuario.Tecnico;
                    return true;
                default:
                    return false;
            }
        }

        private enum TipoRolUsuario
        {
            Ninguno = 0,
            Administrador = 1,
            Gerente = 2,
            JefeMantenimiento = 3,
            Panolero = 4,
            Tecnico = 5
        }
    }
}
