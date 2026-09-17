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
void Rechazar(Action accion, string descripcion)
{
    try { accion(); } catch (ArgumentException) { Console.WriteLine("OK: " + descripcion); return; }
    throw new Exception("No se rechazo: " + descripcion);
}
foreach (var ids in new long[][] { [0], [-1], [99], [2] })
    Rechazar(() => dao.insertarUsuario(Nuevo(99), ids), "Alta rechaza roles invalidos/inexistentes/inactivos");
Verificar(!contexto.Usuarios.Any(), "Altas rechazadas no guardan usuarios");
var antes = contador.Cantidad;
var usuario = dao.insertarUsuario(Nuevo(1), [1, 1]);
Verificar(contador.Cantidad == antes + 1, "Alta ejecuta un solo SaveChanges");
var id = usuario.IdUsuario;
contexto.ChangeTracker.Clear();
usuario = dao.seleccionarUsuario(id)!;
Verificar(usuario.IdRols.Count == 1, "Rol duplicado se normaliza y relacion se persiste");
Verificar(usuario.FechaIngreso == fecha, "Fecha bisiesta persiste sin hora");
var hash = usuario.Contrasena;
Verificar(hash != "Prueba!123" && Contrasenas.Verificar(hash, "Prueba!123") && !Contrasenas.Verificar(hash, "incorrecta"), "Hash acepta clave correcta y rechaza incorrecta");
Verificar(hash != Contrasenas.CrearHash("Prueba!123"), "Hashes usan sal aleatoria");
Verificar(!Contrasenas.Verificar("texto antiguo", "texto antiguo"), "No autentica contrasenas antiguas como hashes");
Rechazar(() => dao.actualizarUsuario(usuario, [99]), "Actualizacion rechaza rol inexistente");
Rechazar(() => dao.actualizarUsuario(usuario, [2]), "Actualizacion rechaza rol inactivo");
Rechazar(() => dao.actualizarUsuario(usuario, [0]), "Actualizacion rechaza rol cero");
Rechazar(() => dao.actualizarUsuario(usuario, [1], " "), "Cambio rechaza contrasena vacia");
usuario.Nombre = "Editado";
Verificar(dao.actualizarUsuario(usuario, [1]), "Actualiza usuario rastreado");
contexto.ChangeTracker.Clear();
usuario = dao.seleccionarUsuario(id)!;
Verificar(usuario.Contrasena == hash && usuario.Nombre == "Editado", "Edicion conserva hash sin duplicarlo");
var desconectado = Nuevo(1); desconectado.IdUsuario = id;
Verificar(dao.actualizarUsuario(desconectado, [3], "Nueva!456"), "Actualiza entidad desconectada y cambia roles y clave");
contexto.ChangeTracker.Clear();
usuario = dao.seleccionarUsuario(id)!;
Verificar(usuario.IdRols.Single().IdRol == 3 && Contrasenas.Verificar(usuario.Contrasena, "Nueva!456"), "Cambio de roles y hash persistido");
hash = usuario.Contrasena;
dao.actualizarUsuario(desconectado, [3]);
contexto.ChangeTracker.Clear();
Verificar(dao.seleccionarUsuario(id)!.Contrasena == hash, "Entidad desconectada no sobrescribe clave sin parametro explicito");
for (var n = 2; n <= 23; n++) dao.insertarUsuario(Nuevo(n), []);
var listado = await dao.ListarTodoAsync(UsuarioMapeador.Proyeccion);
Verificar(listado.Count == 23, "Listado devuelve todos los usuarios sin limite de 20");
Verificar(listado.Select(x => x.Id).SequenceEqual(listado.Select(x => x.Id).Order()), "Listado ordenado por identificador");
var sql = contexto.Usuarios.Select(UsuarioMapeador.Proyeccion).ToQueryString();
Verificar(!sql.Contains("contrasena", StringComparison.OrdinalIgnoreCase), "SQL no selecciona contrasena");
var json = JsonSerializer.Serialize(listado[0], new JsonSerializerOptions(JsonSerializerDefaults.Web));
using var documento = JsonDocument.Parse(json);
Verificar(documento.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet().SetEquals(
    new[] { "id", "nombre", "apellido", "correo", "telefono", "estado", "fechaIngreso", "roles" }), "JSON conserva contrato de salida");
Verificar(documento.RootElement.GetProperty("fechaIngreso").GetString() == "2024-02-29", "JSON fecha ISO sin hora");
Verificar(documento.RootElement.GetProperty("roles")[0].GetProperty("id").GetInt64() == 3, "JSON conserva roles");
Verificar(dao.darBajaLogicaUsuario(id), "Baja logica funciona");
Verificar((await dao.ListarTodoAsync(UsuarioMapeador.Proyeccion))[0].Estado == "inactivo", "DTO refleja estado inactivo");
Verificar(dao.eliminarUsuario(id) && dao.seleccionarUsuario(id) is null, "Eliminacion limpia relaciones");
Console.WriteLine("Todas las comprobaciones pasaron en SQLite en memoria. Sin modificar la base configurada.");

sealed class ContextoPrueba(DbContextOptions<SotraContext> opciones) : SotraContext(opciones)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        // Sustituye las secuencias PostgreSQL solo en esta base efimera.
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
