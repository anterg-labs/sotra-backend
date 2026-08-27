using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

Console.WriteLine("Hello, World!");
ModeloMaquinaDAO opModeloMaquiba = new ModeloMaquinaDAO();

var modelosMaquinas = opModeloMaquiba.listarTodo();

foreach (var aux in modelosMaquinas)
{
    Console.WriteLine($"Id: {aux.IdModeloMaquina}, Nombre: {aux.Nombre}");
}

