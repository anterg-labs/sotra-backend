using ConsolaPruebas;

int opcion;

do
{
    Console.Clear();
    Console.WriteLine("MENU PRINCIPAL");
    Console.WriteLine("1. CRUD ModeloMaquina");
    Console.WriteLine("2. CRUD Usuario");
    Console.WriteLine("0. Salir");
    Console.Write("Seleccione una opción: ");

    string? textoOpcion = Console.ReadLine();
    opcion = int.TryParse(textoOpcion, out int valor) ? valor : -1;

    Console.WriteLine();

    switch (opcion)
    {
        case 1:
            MenuModeloMaquina();
            break;
        case 2:
            MenuUsuario();
            break;
        case 0:
            Console.WriteLine("Saliendo...");
            break;
        default:
            Console.WriteLine("Opción inválida.");
            break;
    }

    if (opcion != 0)
    {
        Console.WriteLine();
        Console.WriteLine("Presione ENTER para continuar...");
        Console.ReadLine();
    }

} while (opcion != 0);

static void MenuModeloMaquina()
{
    int opcion;

    do
    {
        Console.Clear();
        Console.WriteLine("CRUD MODELO MAQUINA");
        Console.WriteLine("1. Listar todo");
        Console.WriteLine("2. Seleccionar por Id");
        Console.WriteLine("3. Insertar");
        Console.WriteLine("4. Actualizar");
        Console.WriteLine("5. Eliminar");
        Console.WriteLine("0. Volver");
        Console.Write("Seleccione una opción: ");

        string? textoOpcion = Console.ReadLine();
        opcion = int.TryParse(textoOpcion, out int valor) ? valor : -1;

        Console.WriteLine();

        switch (opcion)
        {
            case 1:
                testModeloMaquina.ListarTodo();
                break;
            case 2:
                Console.Write("Ingrese el IdModeloMaquina: ");
                testModeloMaquina.SeleccionarPorId(LeerLong());
                break;
            case 3:
                testModeloMaquina.Insertar();
                break;
            case 4:
                testModeloMaquina.Actualizar();
                break;
            case 5:
                testModeloMaquina.Eliminar();
                break;
            case 0:
                break;
            default:
                Console.WriteLine("Opción inválida.");
                break;
        }

        if (opcion != 0)
        {
            Console.WriteLine();
            Console.WriteLine("Presione ENTER para continuar...");
            Console.ReadLine();
        }

    } while (opcion != 0);
}

static void MenuUsuario()
{
    int opcion;

    do
    {
        Console.Clear();
        Console.WriteLine("CRUD USUARIO");
        Console.WriteLine("1. Listar todo");
        Console.WriteLine("2. Seleccionar por Id");
        Console.WriteLine("3. Insertar");
        Console.WriteLine("4. Actualizar");
        Console.WriteLine("5. Eliminar");
        Console.WriteLine("6. Baja lógica (estado=false)");
        Console.WriteLine("0. Volver");
        Console.Write("Seleccione una opción: ");

        string? textoOpcion = Console.ReadLine();
        opcion = int.TryParse(textoOpcion, out int valor) ? valor : -1;

        Console.WriteLine();

        switch (opcion)
        {
            case 1:
                testUsuario.ListarTodo();
                break;
            case 2:
                Console.Write("Ingrese el IdUsuario: ");
                testUsuario.SeleccionarPorId(LeerLong());
                break;
            case 3:
                testUsuario.Insertar();
                break;
            case 4:
                testUsuario.Actualizar();
                break;
            case 5:
                testUsuario.Eliminar();
                break;
            case 6:
                testUsuario.DarBajaLogica();
                break;
            case 0:
                break;
            default:
                Console.WriteLine("Opción inválida.");
                break;
        }

        if (opcion != 0)
        {
            Console.WriteLine();
            Console.WriteLine("Presione ENTER para continuar...");
            Console.ReadLine();
        }

    } while (opcion != 0);
}

static long LeerLong()
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

