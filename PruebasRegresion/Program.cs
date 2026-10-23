using System.Text.Json;
using Api.Mapeadores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

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
await Rechazar(async () => await dao.ActualizarUsuarioAsync(usuario, [99], CancellationToken.None), "Actualizacion rechaza rol inexistente");
await Rechazar(async () => await dao.ActualizarUsuarioAsync(usuario, [2], CancellationToken.None), "Actualizacion rechaza rol inactivo");
await Rechazar(async () => await dao.ActualizarUsuarioAsync(usuario, [0], CancellationToken.None), "Actualizacion rechaza rol cero");
usuario.Nombre = "Editado";
Verificar(await dao.ActualizarUsuarioAsync(usuario, [1], CancellationToken.None), "Actualiza usuario rastreado");
contexto.ChangeTracker.Clear();
usuario = (await dao.SeleccionarUsuarioAsync(id, CancellationToken.None))!;
Verificar(usuario.Nombre == "Editado", "Edicion persiste los cambios");
var desconectado = Nuevo(1); desconectado.IdUsuario = id;
Verificar(await dao.ActualizarUsuarioAsync(desconectado, [3], CancellationToken.None), "Actualiza entidad desconectada y cambia roles");
contexto.ChangeTracker.Clear();
usuario = (await dao.SeleccionarUsuarioAsync(id, CancellationToken.None))!;
Verificar(usuario.IdRols.Single().IdRol == 3, "Cambio de roles persistido");
var idKeycloak = Guid.NewGuid();
Verificar(await dao.SeleccionarOVincularPorKeycloakAsync(idKeycloak, "otro@example.com", CancellationToken.None) is null, "Keycloak sin usuario con ese correo no vincula");
Verificar((await dao.SeleccionarOVincularPorKeycloakAsync(idKeycloak, "U1@EXAMPLE.COM", CancellationToken.None))?.IdUsuario == id, "Keycloak vincula por correo sin distinguir mayusculas");
contexto.ChangeTracker.Clear();
Verificar((await dao.SeleccionarUsuarioAsync(id, CancellationToken.None))!.IdKeycloak == idKeycloak, "Vinculo con Keycloak persistido");
Verificar((await dao.SeleccionarOVincularPorKeycloakAsync(idKeycloak, null, CancellationToken.None))?.IdUsuario == id, "Usuario vinculado se encuentra por id de Keycloak sin correo");
Verificar(await dao.SeleccionarOVincularPorKeycloakAsync(Guid.NewGuid(), "u1@example.com", CancellationToken.None) is null, "Otro id de Keycloak no toma un usuario ya vinculado");
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
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    { Cantidad++; return ValueTask.FromResult(result); }
}
