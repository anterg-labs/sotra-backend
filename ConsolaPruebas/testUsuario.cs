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

        usuario.FechaIngreso = DateTime.UtcNow;

        string tipoUsuario = LeerTipoUsuario();

        usuario = dao.insertarUsuario(usuario, tipoUsuario);

        Console.WriteLine("Usuario insertado correctamente.");
        Console.WriteLine(usuario);
        Console.WriteLine($"Rol: {tipoUsuario}");
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

        string rolActual = ObtenerRol(usuario);
        string tipoUsuario = LeerTipoUsuario(rolActual);

        bool actualizado = dao.actualizarUsuario(usuario, tipoUsuario);
        Console.WriteLine(actualizado ? "Usuario actualizado correctamente." : "No se pudo actualizar el registro.");
        if (actualizado)
        {
            Console.WriteLine(usuario);
            Console.WriteLine($"Rol: {tipoUsuario}");
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

    private static string LeerTipoUsuario(string valorPorDefecto = "Ninguno")
    {
        while (true)
        {
            Console.WriteLine("Seleccione tipo de usuario:");
            Console.WriteLine($"1. Administrador{MostrarDefault(valorPorDefecto, "Administrador")}");
            Console.WriteLine($"2. Gerente{MostrarDefault(valorPorDefecto, "Gerente")}");
            Console.WriteLine($"3. JefeMantenimiento{MostrarDefault(valorPorDefecto, "JefeMantenimiento")}");
            Console.WriteLine($"4. Panolero{MostrarDefault(valorPorDefecto, "Panolero")}");
            Console.WriteLine($"5. Tecnico{MostrarDefault(valorPorDefecto, "Tecnico")}");
            Console.WriteLine($"0. Ninguno{MostrarDefault(valorPorDefecto, "Ninguno")}");
            Console.Write("Opción: ");

            string? texto = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(texto) && valorPorDefecto != "Ninguno")
            {
                return valorPorDefecto;
            }

            if (texto is "0") return "Ninguno";
            if (texto is "1") return "Administrador";
            if (texto is "2") return "Gerente";
            if (texto is "3") return "JefeMantenimiento";
            if (texto is "4") return "Panolero";
            if (texto is "5") return "Tecnico";

            Console.WriteLine("Opción inválida.");
        }
    }

    private static string MostrarDefault(string valorPorDefecto, string valor)
    {
        return valorPorDefecto == valor ? " (actual)" : string.Empty;
    }

    private static string ObtenerRol(Usuario usuario)
    {
        if (usuario.Administrador is not null) return "Administrador";
        if (usuario.Gerente is not null) return "Gerente";
        if (usuario.JefeMantenimiento is not null) return "JefeMantenimiento";
        if (usuario.Panolero is not null) return "Panolero";
        if (usuario.Tecnico is not null) return "Tecnico";
        return "Ninguno";
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
