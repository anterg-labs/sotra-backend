using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class UsuariosPruebas : PruebaBase
{
    [Test]
    public async Task ListarUsuariosDevuelveUnaPaginaConElFormatoEstandar()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/usuarios");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var pagina = await respuesta.LeerJsonAsync();
        Assert.That(pagina.GetProperty("pagina").GetInt32(), Is.EqualTo(1));
        Assert.That(pagina.GetProperty("tamanoPagina").GetInt32(), Is.EqualTo(20));
        Assert.That(pagina.GetProperty("totalElementos").GetInt32(), Is.GreaterThan(0));
        Assert.That(pagina.GetProperty("totalPaginas").GetInt32(), Is.GreaterThan(0));

        var primero = pagina.GetProperty("elementos")[0];
        var propiedades = primero.EnumerateObject().Select(propiedad => propiedad.Name).ToList();
        Assert.That(propiedades, Is.EquivalentTo(new[]
        {
            "id", "nombre", "apellido", "correo", "telefono", "estado", "fechaIngreso", "roles", "cantidadOrdenesAsignadas",
        }));
    }

    [Test]
    public async Task ListarUsuariosRechazaLaPaginaCero()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/usuarios?pagina=0");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("PAGINACION_INVALIDA"));
    }

    [Test]
    public async Task ListarUsuariosRechazaUnTamanoDePaginaMayorACien()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/usuarios?tamanoPagina=101");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("PAGINACION_INVALIDA"));
    }

    [Test]
    public async Task ListarUsuariosFiltraPorEstado()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/usuarios?estado=activo&tamanoPagina=100");

        var pagina = await respuesta.LeerJsonAsync();
        var estados = pagina.GetProperty("elementos").EnumerateArray()
            .Select(usuario => usuario.GetProperty("estado").GetString()).Distinct().ToList();
        Assert.That(estados, Is.EqualTo(new[] { "activo" }));
    }

    [Test]
    public async Task ListarUsuariosBuscaPorCorreo()
    {
        var usuario = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var correo = usuario.GetProperty("correo").GetString()!;

        var respuesta = await Cliente.GetAsync("/api/v1/usuarios?buscar=" + Uri.EscapeDataString(correo));

        var pagina = await respuesta.LeerJsonAsync();
        Assert.That(pagina.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task ObtenerUnUsuarioInexistenteResponde404ConProblemDetailsYCodigo()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/usuarios/999999999");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("status").GetInt32(), Is.EqualTo(404));
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("RECURSO_NO_ENCONTRADO"));
    }

    [Test]
    public async Task CrearUsuarioResponde201ConLocationYEstadoActivo()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", DatosDePrueba.UsuarioNuevo());

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var usuario = await respuesta.LeerJsonAsync();
        var id = usuario.GetProperty("id").GetInt64();
        Assert.That(respuesta.Headers.Location!.ToString(), Does.EndWith("/api/v1/usuarios/" + id));
        Assert.That(usuario.GetProperty("estado").GetString(), Is.EqualTo("activo"));
        Assert.That(usuario.GetProperty("roles").GetArrayLength(), Is.EqualTo(1));
        Assert.That(usuario.TryGetProperty("contrasena", out _), Is.False);

        var fechaIngreso = DateOnly.Parse(usuario.GetProperty("fechaIngreso").GetString()!);
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        Assert.That(Math.Abs(hoy.DayNumber - fechaIngreso.DayNumber), Is.LessThanOrEqualTo(1));
    }

    [Test]
    public async Task CrearUsuarioSinRolesLoCreaSinRoles()
    {
        var datos = DatosDePrueba.UsuarioNuevo(idsRol: Array.Empty<long>());

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var usuario = await respuesta.LeerJsonAsync();
        Assert.That(usuario.GetProperty("roles").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task CrearUsuarioConCamposInvalidosResponde400ConErroresPorCampo()
    {
        var datos = new { nombre = "", apellido = "", correo = "no-es-un-correo", telefono = "" };

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        var campos = cuerpo.GetProperty("errors").EnumerateObject().Select(propiedad => propiedad.Name).ToList();
        Assert.That(campos, Does.Contain("Nombre"));
        Assert.That(campos, Does.Contain("Apellido"));
        Assert.That(campos, Does.Contain("Correo"));
        Assert.That(campos, Does.Contain("Telefono"));
    }

    [Test]
    public async Task CrearUsuarioConCorreoRepetidoResponde409()
    {
        var primero = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var correo = primero.GetProperty("correo").GetString()!;

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", DatosDePrueba.UsuarioNuevo(correo: correo));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("CORREO_DUPLICADO"));
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("El correo electrónico ya está registrado."));
    }

    [Test]
    public async Task CrearUsuarioConTelefonoRepetidoResponde409()
    {
        var primero = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var telefono = primero.GetProperty("telefono").GetString()!;

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", DatosDePrueba.UsuarioNuevo(telefono: telefono));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo("TELEFONO_DUPLICADO"));
    }

    [Test]
    public async Task CrearUsuarioConUnRolInexistenteResponde400ConMensaje()
    {
        var datos = DatosDePrueba.UsuarioNuevo(idsRol: new long[] { 999999 });

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("Los siguientes roles no existen: 999999"));
    }

    [Test]
    public async Task CrearUsuarioConUnIdDeRolNoPositivoResponde400ConMensaje()
    {
        var datos = DatosDePrueba.UsuarioNuevo(idsRol: new long[] { 0 });

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("Los ids de rol deben ser positivos."));
    }

    [Test]
    public async Task CrearUsuarioRechazaUnTelefonoConLetras()
    {
        var datos = DatosDePrueba.UsuarioNuevo(telefono: "abc" + DatosDePrueba.Telefono());

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("", "Perez")]
    [TestCase("   ", "Perez")]
    [TestCase("###", "Perez")]
    [TestCase("Juan3", "Perez")]
    [TestCase("Juan", "")]
    [TestCase("Juan", "***")]
    public async Task CrearUsuarioRechazaNombresYApellidosInvalidos(string nombre, string apellido)
    {
        var datos = new
        {
            nombre,
            apellido,
            correo = "prueba." + DatosDePrueba.Sufijo() + "@pruebas.test",
            telefono = DatosDePrueba.Telefono(),
            idsRol = new long[] { 1 },
        };

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase(1)]
    [TestCase(100)]
    public async Task CrearUsuarioAceptaNombreYApellidoEnLosLimitesValidos(int largo)
    {
        var texto = new string('a', largo);
        var datos = new
        {
            nombre = texto,
            apellido = texto,
            correo = "prueba." + DatosDePrueba.Sufijo() + "@pruebas.test",
            telefono = DatosDePrueba.Telefono(),
            idsRol = new long[] { 1 },
        };

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/usuarios", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task ActualizarUsuarioReemplazaLosCamposYLosRoles()
    {
        var usuario = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var id = usuario.GetProperty("id").GetInt64();
        var datos = new
        {
            nombre = "Nombre nuevo",
            apellido = "Apellido nuevo",
            correo = usuario.GetProperty("correo").GetString(),
            telefono = usuario.GetProperty("telefono").GetString(),
            estado = "activo",
            fechaIngreso = "2026-08-12",
            idsRol = new long[] { 2, 3 },
        };

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/usuarios/" + id, datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualizado = await respuesta.LeerJsonAsync();
        Assert.That(actualizado.GetProperty("nombre").GetString(), Is.EqualTo("Nombre nuevo"));
        Assert.That(actualizado.GetProperty("fechaIngreso").GetString(), Is.EqualTo("2026-08-12"));
        var roles = actualizado.GetProperty("roles").EnumerateArray().Select(rol => rol.GetProperty("id").GetInt64()).ToList();
        Assert.That(roles, Is.EqualTo(new long[] { 2, 3 }));
    }

    [Test]
    public async Task ActualizarUsuarioConListaDeRolesVaciaLeQuitaTodosLosRoles()
    {
        var usuario = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var id = usuario.GetProperty("id").GetInt64();
        var datos = new
        {
            nombre = "Prueba",
            apellido = "Usuario",
            correo = usuario.GetProperty("correo").GetString(),
            telefono = usuario.GetProperty("telefono").GetString(),
            estado = "activo",
            fechaIngreso = "2026-08-12",
            idsRol = Array.Empty<long>(),
        };

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/usuarios/" + id, datos);

        var actualizado = await respuesta.LeerJsonAsync();
        Assert.That(actualizado.GetProperty("roles").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task ActualizarUsuarioInexistenteResponde404()
    {
        var datos = new
        {
            nombre = "Prueba",
            apellido = "Usuario",
            correo = "inexistente@pruebas.test",
            telefono = "12345678",
            estado = "activo",
            fechaIngreso = "2026-08-12",
            idsRol = Array.Empty<long>(),
        };

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/usuarios/999999999", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ActualizarUsuarioConUnEstadoInvalidoResponde400ConMensaje()
    {
        var usuario = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var id = usuario.GetProperty("id").GetInt64();
        var datos = new
        {
            nombre = "Prueba",
            apellido = "Usuario",
            correo = usuario.GetProperty("correo").GetString(),
            telefono = usuario.GetProperty("telefono").GetString(),
            estado = "suspendido",
            fechaIngreso = "2026-08-12",
            idsRol = new long[] { 1 },
        };

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/usuarios/" + id, datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("El estado debe ser 'activo' o 'inactivo'."));
    }

    [Test]
    public async Task ActualizarUsuarioSinLosCamposObligatoriosResponde400()
    {
        var usuario = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var id = usuario.GetProperty("id").GetInt64();

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/usuarios/" + id, new { nombre = "Solo el nombre" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task DesactivarUsuarioResponde200YEsIdempotente()
    {
        var usuario = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var id = usuario.GetProperty("id").GetInt64();
        var cuerpo = new { estado = "inactivo" };

        var primera = await Cliente.PatchAsJsonAsync("/api/v1/usuarios/" + id, cuerpo);
        var segunda = await Cliente.PatchAsJsonAsync("/api/v1/usuarios/" + id, cuerpo);

        Assert.That(primera.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(segunda.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var desactivado = await segunda.LeerJsonAsync();
        Assert.That(desactivado.GetProperty("estado").GetString(), Is.EqualTo("inactivo"));
    }

    [Test]
    public async Task DesactivarUsuarioConOtroEstadoResponde400ConMensaje()
    {
        var usuario = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var id = usuario.GetProperty("id").GetInt64();

        var respuesta = await Cliente.PatchAsJsonAsync("/api/v1/usuarios/" + id, new { estado = "activo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("Este endpoint solo acepta el estado 'inactivo'."));
    }

    [Test]
    public async Task DesactivarUsuarioInexistenteResponde404()
    {
        var respuesta = await Cliente.PatchAsJsonAsync("/api/v1/usuarios/999999999", new { estado = "inactivo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task UnUsuarioInactivoSeReactivaConPutYEstadoActivo()
    {
        var usuario = await DatosDePrueba.CrearUsuarioAsync(Cliente);
        var id = usuario.GetProperty("id").GetInt64();
        await Cliente.PatchAsJsonAsync("/api/v1/usuarios/" + id, new { estado = "inactivo" });
        var datos = new
        {
            nombre = "Prueba",
            apellido = "Usuario",
            correo = usuario.GetProperty("correo").GetString(),
            telefono = usuario.GetProperty("telefono").GetString(),
            estado = "activo",
            fechaIngreso = "2026-08-12",
            idsRol = new long[] { 1 },
        };

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/usuarios/" + id, datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var reactivado = await respuesta.LeerJsonAsync();
        Assert.That(reactivado.GetProperty("estado").GetString(), Is.EqualTo("activo"));
    }
}
