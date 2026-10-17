using System.Text.Json;
using Api.Mapeadores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;
using SOTRA_BACKEND.Seguridad;

var contador = new ContadorGuardados();
using var contexto = new ContextoPrueba(new DbContextOptionsBuilder<SotraContext>()
    .UseSqlite("Data Source=:memory:").AddInterceptors(contador).Options);
contexto.Database.OpenConnection();
contexto.Database.EnsureCreated();
contexto.Rols.AddRange(new Rol { IdRol = 1, Nombre = "Activo", Activo = true },
    new Rol { IdRol = 2, Nombre = "Inactivo", Activo = false },
    new Rol { IdRol = 3, Nombre = "Otro", Activo = true });
contexto.SaveChanges();
var dao = new UsuarioDAO(contexto);
var fecha = new DateOnly(2024, 2, 29);
Usuario Nuevo(int n) => new()
{
    Nombre = "Prueba",
    Apellido = "Fecha",
    Mail = $"u{n}@example.com",
    Telefono = n.ToString(),
    Contrasena = "Prueba!123",
    Estado = true,
    FechaIngreso = fecha
};
void Verificar(bool condicion, string descripcion)
{
    if (!condicion) throw new Exception(descripcion);
    Console.WriteLine("OK: " + descripcion);
}
async Task Rechazar(Func<Task> accion, string descripcion)
{
    try { await accion(); } catch (ArgumentException) { Console.WriteLine("OK: " + descripcion); return; }
    throw new Exception("No se rechazo: " + descripcion);
}
foreach (var ids in new long[][] { [0], [-1], [99], [2] })
    await Rechazar(async () => await dao.InsertarUsuarioAsync(Nuevo(99), ids, CancellationToken.None), "Alta rechaza roles invalidos/inexistentes/inactivos");
Verificar(!contexto.Usuarios.Any(), "Altas rechazadas no guardan usuarios");
var antes = contador.Cantidad;
var usuario = await dao.InsertarUsuarioAsync(Nuevo(1), [1, 1], CancellationToken.None);
Verificar(contador.Cantidad == antes + 1, "Alta ejecuta un solo SaveChanges");
var id = usuario.IdUsuario;
contexto.ChangeTracker.Clear();
usuario = (await dao.SeleccionarUsuarioAsync(id, CancellationToken.None))!;
Verificar(usuario.IdRols.Count == 1, "Rol duplicado se normaliza y relacion se persiste");
Verificar(usuario.FechaIngreso == fecha, "Fecha bisiesta persiste sin hora");
var hash = usuario.Contrasena;
Verificar(hash != "Prueba!123" && Contrasenas.Verificar(hash, "Prueba!123") && !Contrasenas.Verificar(hash, "incorrecta"), "Hash acepta clave correcta y rechaza incorrecta");
Verificar(hash != Contrasenas.CrearHash("Prueba!123"), "Hashes usan sal aleatoria");
Verificar(!Contrasenas.Verificar("texto antiguo", "texto antiguo"), "No autentica contrasenas antiguas como hashes");
await Rechazar(async () => await dao.ActualizarUsuarioAsync(usuario, [99], null, CancellationToken.None), "Actualizacion rechaza rol inexistente");
await Rechazar(async () => await dao.ActualizarUsuarioAsync(usuario, [2], null, CancellationToken.None), "Actualizacion rechaza rol inactivo");
await Rechazar(async () => await dao.ActualizarUsuarioAsync(usuario, [0], null, CancellationToken.None), "Actualizacion rechaza rol cero");
await Rechazar(async () => await dao.ActualizarUsuarioAsync(usuario, [1], " ", CancellationToken.None), "Cambio rechaza contrasena vacia");
usuario.Nombre = "Editado";
Verificar(await dao.ActualizarUsuarioAsync(usuario, [1], null, CancellationToken.None), "Actualiza usuario rastreado");
contexto.ChangeTracker.Clear();
usuario = (await dao.SeleccionarUsuarioAsync(id, CancellationToken.None))!;
Verificar(usuario.Contrasena == hash && usuario.Nombre == "Editado", "Edicion conserva hash sin duplicarlo");
var desconectado = Nuevo(1); desconectado.IdUsuario = id;
Verificar(await dao.ActualizarUsuarioAsync(desconectado, [3], "Nueva!456", CancellationToken.None), "Actualiza entidad desconectada y cambia roles y clave");
contexto.ChangeTracker.Clear();
usuario = (await dao.SeleccionarUsuarioAsync(id, CancellationToken.None))!;
Verificar(usuario.IdRols.Single().IdRol == 3 && Contrasenas.Verificar(usuario.Contrasena, "Nueva!456"), "Cambio de roles y hash persistido");
hash = usuario.Contrasena;
await dao.ActualizarUsuarioAsync(desconectado, [3], null, CancellationToken.None);
contexto.ChangeTracker.Clear();
Verificar((await dao.SeleccionarUsuarioAsync(id, CancellationToken.None))!.Contrasena == hash, "Entidad desconectada no sobrescribe clave sin parametro explicito");
for (var n = 2; n <= 23; n++) await dao.InsertarUsuarioAsync(Nuevo(n), [], CancellationToken.None);
var listado = (await dao.ListarTodoAsync(CancellationToken.None)).Select(UsuarioMapeador.AListadoDto).ToList();
Verificar(listado.Count == 23, "Listado devuelve todos los usuarios sin limite de 20");
Verificar(listado.Select(x => x.Id).SequenceEqual(listado.Select(x => x.Id).Order()), "Listado ordenado por identificador");
var json = JsonSerializer.Serialize(listado[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));
using var documento = JsonDocument.Parse(json);
Verificar(documento.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(
    new[] { "id", "nombre", "apellido", "correo", "telefono", "estado", "fechaIngreso", "roles" }), "JSON conserva contrato de salida");
Verificar(documento.RootElement.GetProperty("fechaIngreso").GetString() == "2024-02-29", "JSON fecha ISO sin hora");
Verificar(documento.RootElement.GetProperty("roles")[0].GetProperty("id").GetInt64() == 3, "JSON conserva roles");
Verificar(await dao.DarBajaLogicaUsuarioAsync(id, CancellationToken.None), "Baja logica funciona");
Verificar(UsuarioMapeador.AListadoDto((await dao.ListarTodoAsync(CancellationToken.None))[0]).Estado == "inactivo", "DTO refleja estado inactivo");
Verificar(await dao.EliminarUsuarioAsync(id, CancellationToken.None) && await dao.SeleccionarUsuarioAsync(id, CancellationToken.None) is null, "Eliminacion limpia relaciones");
Console.WriteLine("Todas las comprobaciones pasaron en SQLite en memoria. Sin modificar la base configurada.");

sealed class ContextoPrueba(DbContextOptions<SotraContext> opciones) : SotraContext(opciones)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entidad in modelBuilder.Model.GetEntityTypes())
            foreach (var propiedad in entidad.GetProperties())
                if (propiedad.GetDefaultValueSql() is not null) propiedad.SetDefaultValueSql(null);
    }
}
sealed class ContadorGuardados : SaveChangesInterceptor
{
    public int Cantidad { get; private set; }
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    { Cantidad++; return result; }
}
