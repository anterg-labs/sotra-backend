using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace ConsolaPruebas;

public static class testUsuario
{
    public static void ListarTodo()
    {
        UsuarioDAO dao = new UsuarioDAO();
        var usuarios = dao.listarTodo();

        if (usuarios.Count == 0)
        {
            Console.WriteLine("No hay registros de Usuario.");
            return;
        }

        foreach (var usuario in usuarios)
        {
            Console.WriteLine(usuario);
            Console.WriteLine($"Rol: {ObtenerRol(usuario)}");
            Console.WriteLine();
        }
    }

    public static void SeleccionarPorId(long id)
    {
        UsuarioDAO dao = new UsuarioDAO();
        Usuario? usuario = dao.seleccionarUsuario(id);

        if (usuario is null)
        {
            Console.WriteLine($"No se encontró un Usuario con Id: {id}");
            return;
        }

        Console.WriteLine(usuario);
        Console.WriteLine($"Rol: {ObtenerRol(usuario)}");
    }

    public static void Insertar()
    {
        UsuarioDAO dao = new UsuarioDAO();

        Usuario usuario = new Usuario();

        Console.Write("Nombre: ");
        usuario.Nombre = Console.ReadLine() ?? string.Empty;

        Console.Write("Apellido: ");
        usuario.Apellido = Console.ReadLine() ?? string.Empty;

        Console.Write("Mail: ");
        usuario.Mail = Console.ReadLine() ?? string.Empty;

        Console.Write("Telefono: ");
        usuario.Telefono = Console.ReadLine() ?? string.Empty;

        Console.Write("Contrasena: ");
        usuario.Contrasena = Console.ReadLine() ?? string.Empty;

        Console.Write("Estado (true/false): ");
        usuario.Estado = LeerBool();

        usuario.FechaIngreso = TimeOnly.FromDateTime(DateTime.Now);

        List<long> idsRol = LeerRolesUsuario();

        usuario = dao.insertarUsuario(usuario, idsRol);

        Console.WriteLine("Usuario insertado correctamente.");
        Console.WriteLine(usuario);
        Console.WriteLine($"Roles: {(idsRol.Count == 0 ? "Ninguno" : string.Join(", ", idsRol))}");
    }

    public static void Actualizar()
    {
        UsuarioDAO dao = new UsuarioDAO();

        Console.Write("IdUsuario a actualizar: ");
        long id = LeerLong();

        Usuario? usuario = dao.seleccionarUsuario(id);
        if (usuario is null)
        {
            Console.WriteLine($"No se encontró un Usuario con Id: {id}");
            return;
        }

        Console.Write($"Nombre ({usuario.Nombre}): ");
        string? nombre = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            usuario.Nombre = nombre;
        }

        Console.Write($"Apellido ({usuario.Apellido}): ");
        string? apellido = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(apellido))
        {
            usuario.Apellido = apellido;
        }

        Console.Write($"Mail ({usuario.Mail}): ");
        string? mail = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(mail))
        {
            usuario.Mail = mail;
        }

        Console.Write($"Telefono ({usuario.Telefono}): ");
        string? telefono = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(telefono))
        {
            usuario.Telefono = telefono;
        }

        Console.Write($"Contrasena ({usuario.Contrasena}): ");
        string? contrasena = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(contrasena))
        {
            usuario.Contrasena = contrasena;
        }

        Console.Write($"Estado ({usuario.Estado}): ");
        string? estado = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(estado) && bool.TryParse(estado, out bool estadoBool))
        {
            usuario.Estado = estadoBool;
        }

        List<long> idsRolActuales = usuario.IdRols.Select(r => r.IdRol).ToList();
        List<long> idsRol = LeerRolesUsuario(idsRolActuales);

        bool actualizado = dao.actualizarUsuario(usuario, idsRol);
        Console.WriteLine(actualizado ? "Usuario actualizado correctamente." : "No se pudo actualizar el registro.");
        if (actualizado)
        {
            Console.WriteLine(usuario);
            Console.WriteLine($"Roles: {(idsRol.Count == 0 ? "Ninguno" : string.Join(", ", idsRol))}");
        }
    }

    public static void Eliminar()
    {
        UsuarioDAO dao = new UsuarioDAO();

        Console.Write("IdUsuario a eliminar: ");
        long id = LeerLong();

        bool eliminado = dao.eliminarUsuario(id);
        Console.WriteLine(eliminado ? "Usuario eliminado correctamente." : $"No se encontró un Usuario con Id: {id}");
    }

    public static void DarBajaLogica()
    {
        UsuarioDAO dao = new UsuarioDAO();

        Console.Write("IdUsuario a dar de baja lógica: ");
        long id = LeerLong();

        bool dadoDeBaja = dao.darBajaLogicaUsuario(id);
        Console.WriteLine(dadoDeBaja ? "Usuario dado de baja lógica correctamente." : $"No se encontró un Usuario con Id: {id}");
    }

    private static List<long> LeerRolesUsuario(IEnumerable<long>? valoresPorDefecto = null)
    {
        var idsPorDefecto = (valoresPorDefecto ?? Enumerable.Empty<long>())
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        while (true)
        {
            Console.WriteLine("Ingrese ids de rol separados por coma (ej: 1,3):");
            Console.WriteLine("1. Administrador");
            Console.WriteLine("2. Jefe de mantenimiento");
            Console.WriteLine("3. Técnico");
            Console.WriteLine("4. Pañolero");
            Console.WriteLine("0. Sin roles");

            if (idsPorDefecto.Count > 0)
            {
                Console.WriteLine($"Actual: {string.Join(", ", idsPorDefecto)}");
            }

            Console.Write("Ids de rol: ");
            string? texto = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(texto))
            {
                return idsPorDefecto;
            }

            if (texto.Trim() == "0")
            {
                return new List<long>();
            }

            string[] partes = texto.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var ids = new List<long>();
            bool invalido = false;

            foreach (var parte in partes)
            {
                if (!long.TryParse(parte, out long id) || id <= 0)
                {
                    invalido = true;
                    break;
                }

                ids.Add(id);
            }

            if (invalido)
            {
                Console.WriteLine("Formato inválido. Ingrese ids numéricos positivos separados por coma.");
                continue;
            }

            return ids
                .Distinct()
                .OrderBy(id => id)
                .ToList();
        }
    }

    private static string ObtenerRol(Usuario usuario)
    {
        if (usuario.IdRols is null || usuario.IdRols.Count == 0)
        {
            return "Ninguno";
        }

        return string.Join(", ", usuario.IdRols.Select(r => r.Nombre));
    }

    private static long LeerLong()
    {
        while (true)
        {
            string? texto = Console.ReadLine();
            if (long.TryParse(texto, out long valor))
            {
                return valor;
            }

            Console.Write("Valor inválido. Intente nuevamente: ");
        }
    }

    private static bool LeerBool()
    {
        while (true)
        {
            string? texto = Console.ReadLine();
            if (bool.TryParse(texto, out bool valor))
            {
                return valor;
            }

            Console.Write("Valor inválido. Escriba true o false: ");
        }
    }
}
