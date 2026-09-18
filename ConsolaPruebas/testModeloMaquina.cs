using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace ConsolaPruebas;

public static class testModeloMaquina
{
    public static void ListarTodo()
    {
        ModeloMaquinaDAO dao = new ModeloMaquinaDAO();
        var modelosMaquinas = dao.listarTodo();

        if (modelosMaquinas.Count == 0)
        {
            Console.WriteLine("No hay registros de ModeloMaquina.");
            return;
        }

        foreach (var modelo in modelosMaquinas)
        {
            Console.WriteLine(modelo);
        }
    }

    public static void SeleccionarPorId(long id)
    {
        ModeloMaquinaDAO dao = new ModeloMaquinaDAO();
        ModeloMaquina? modelo = dao.seleccionarModeloMaquina(id);

        if (modelo is null)
        {
            Console.WriteLine($"No se encontró un ModeloMaquina con Id: {id}");
            return;
        }

        Console.WriteLine(modelo);
    }

    public static void Insertar()
    {
        ModeloMaquinaDAO dao = new ModeloMaquinaDAO();

        ModeloMaquina modelo = new ModeloMaquina();

        Console.Write("Nombre: ");
        modelo.Nombre = Console.ReadLine() ?? string.Empty;

        Console.Write("IdTipoMaquina: ");
        modelo.IdTipoMaquina = LeerLong();

        Console.Write("Descripcion: ");
        modelo.Descripcion = Console.ReadLine();

        Console.Write("Activo (true/false): ");
        modelo.Activo = LeerBool();

        dao.insertarModeloMaquina(modelo);
        Console.WriteLine("ModeloMaquina insertado correctamente.");
        Console.WriteLine(modelo);
    }

    public static void Actualizar()
    {
        ModeloMaquinaDAO dao = new ModeloMaquinaDAO();

        Console.Write("IdModeloMaquina a actualizar: ");
        long id = LeerLong();

        ModeloMaquina? modelo = dao.seleccionarModeloMaquina(id);
        if (modelo is null)
        {
            Console.WriteLine($"No se encontró un ModeloMaquina con Id: {id}");
            return;
        }

        Console.Write($"Nombre ({modelo.Nombre}): ");
        string? nombre = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(nombre))
        {
            modelo.Nombre = nombre;
        }

        Console.Write($"IdTipoMaquina ({modelo.IdTipoMaquina}): ");
        string? idTipo = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(idTipo) && long.TryParse(idTipo, out long idTipoMaquina))
        {
            modelo.IdTipoMaquina = idTipoMaquina;
        }

        Console.Write($"Descripcion ({modelo.Descripcion}): ");
        string? descripcion = Console.ReadLine();
        if (descripcion is not null)
        {
            modelo.Descripcion = descripcion;
        }

        Console.Write($"Activo ({modelo.Activo}): ");
        string? activo = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(activo) && bool.TryParse(activo, out bool valorActivo))
        {
            modelo.Activo = valorActivo;
        }

        bool actualizado = dao.actualizarModeloMaquina(modelo);
        Console.WriteLine(actualizado ? "ModeloMaquina actualizado correctamente." : "No se pudo actualizar el registro.");
        if (actualizado)
        {
            Console.WriteLine(modelo);
        }
    }

    public static void Eliminar()
    {
        ModeloMaquinaDAO dao = new ModeloMaquinaDAO();

        Console.Write("IdModeloMaquina a eliminar: ");
        long id = LeerLong();

        bool eliminado = dao.eliminarModeloMaquina(id);
        Console.WriteLine(eliminado ? "ModeloMaquina eliminado correctamente." : $"No se encontró un ModeloMaquina con Id: {id}");
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
