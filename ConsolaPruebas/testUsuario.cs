using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace ConsolaPruebas;

public static class testUsuario
{
    public static async Task ListarTodo()
    {
        UsuarioDAO dao = new UsuarioDAO();
        var usuarios = await dao.ListarTodoAsync(CancellationToken.None);

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

    public static async Task SeleccionarPorId(long id)
    {
        UsuarioDAO dao = new UsuarioDAO();
        Usuario? usuario = await dao.SeleccionarUsuarioAsync(id, CancellationToken.None);

        if (usuario is null)
        {
            Console.WriteLine($"No se encontró un Usuario con Id: {id}");
            return;
        }

        Console.WriteLine(usuario);
        Console.WriteLine($"Rol: {ObtenerRol(usuario)}");
    }

    public static async Task Insertar()
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
        usuario.Contrasena = LeerContrasena();

        Console.Write("Estado (true/false): ");
        usuario.Estado = LeerBool();

        usuario.FechaIngreso = DateOnly.FromDateTime(DateTime.Now);

        List<long> idsRol = LeerRolesUsuario();

        usuario = await dao.InsertarUsuarioAsync(usuario, idsRol, CancellationToken.None);

        Console.WriteLine("Usuario insertado correctamente.");
        Console.WriteLine(usuario);
        Console.WriteLine($"Roles: {(idsRol.Count == 0 ? "Ninguno" : string.Join(", ", idsRol))}");
    }

    public static async Task Actualizar()
    {
        UsuarioDAO dao = new UsuarioDAO();

        Console.Write("IdUsuario a actualizar: ");
        long id = LeerLong();

        Usuario? usuario = await dao.SeleccionarUsuarioAsync(id, CancellationToken.None);
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

        Console.Write("Nueva contrasena (ENTER para conservar): ");
        string contrasena = LeerContrasena();

        Console.Write($"Estado ({usuario.Estado}): ");
        string? estado = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(estado) && bool.TryParse(estado, out bool estadoBool))
        {
            usuario.Estado = estadoBool;
        }

        List<long> idsRolActuales = usuario.IdRols.Select(r => r.IdRol).ToList();
        List<long> idsRol = LeerRolesUsuario(idsRolActuales);

        bool actualizado = await dao.ActualizarUsuarioAsync(usuario, idsRol, contrasena.Length == 0 ? null : contrasena, CancellationToken.None);
        Console.WriteLine(actualizado ? "Usuario actualizado correctamente." : "No se pudo actualizar el registro.");
        if (actualizado)
        {
            Console.WriteLine(usuario);
            Console.WriteLine($"Roles: {(idsRol.Count == 0 ? "Ninguno" : string.Join(", ", idsRol))}");
        }
    }

    public static async Task Eliminar()
    {
        UsuarioDAO dao = new UsuarioDAO();

        Console.Write("IdUsuario a eliminar: ");
        long id = LeerLong();

        bool eliminado = await dao.EliminarUsuarioAsync(id, CancellationToken.None);
        Console.WriteLine(eliminado ? "Usuario eliminado correctamente." : $"No se encontró un Usuario con Id: {id}");
    }

    public static async Task DarBajaLogica()
    {
        UsuarioDAO dao = new UsuarioDAO();

        Console.Write("IdUsuario a dar de baja lógica: ");
        long id = LeerLong();

        bool dadoDeBaja = await dao.DarBajaLogicaUsuarioAsync(id, CancellationToken.None);
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

    private static string LeerContrasena()
    {
        if (Console.IsInputRedirected) return Console.ReadLine() ?? string.Empty;
        var texto = new System.Text.StringBuilder();
        while (true)
        {
            var tecla = Console.ReadKey(intercept: true);
            if (tecla.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return texto.ToString();
            }
            if (tecla.Key == ConsoleKey.Backspace)
            {
                if (texto.Length > 0) texto.Length--;
            }
            else if (!char.IsControl(tecla.KeyChar)) texto.Append(tecla.KeyChar);
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
