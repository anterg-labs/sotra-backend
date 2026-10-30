using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using SOTRA_BACKEND.Operaciones;

namespace Api.Pruebas.Caracterizacion;

public class AuditoriaPruebas
{
    private sealed class RegistroEnMemoria : ILogger<AuditoriaDeAcciones>
    {
        public List<string> Mensajes { get; } = [];

        public IDisposable? BeginScope<TState>(TState estado) where TState : notnull => null;

        public bool IsEnabled(LogLevel nivel) => true;

        public void Log<TState>(LogLevel nivel, EventId evento, TState estado, Exception? excepcion, Func<TState, Exception?, string> formateador)
        {
            Mensajes.Add(formateador(estado, excepcion));
        }
    }

    private sealed class UsuarioDePrueba(string? correo) : IUsuarioActual
    {
        public bool EstaAutenticado => correo is not null;

        public string? Correo => correo;
    }

    private static async Task<List<string>> EjecutarAsync(string metodo, IActionResult resultado, string? correo = null,
        string? idDelRecurso = "7", Exception? excepcion = null, (string Clave, string Valor)? idsDeRuta = null)
    {
        var registro = new RegistroEnMemoria();
        var filtro = new AuditoriaDeAcciones(registro, new UsuarioDePrueba(correo));
        var contextoHttp = new DefaultHttpContext();
        contextoHttp.Request.Method = metodo;
        var rutas = new RouteData();
        rutas.Values["controller"] = "Clientes";
        rutas.Values["action"] = "Actualizar";
        if (idDelRecurso is not null)
        {
            rutas.Values["id"] = idDelRecurso;
        }

        if (idsDeRuta is { } extra)
        {
            rutas.Values[extra.Clave] = extra.Valor;
        }

        var contextoDeAccion = new ActionContext(contextoHttp, rutas, new ActionDescriptor());
        var ejecutando = new ActionExecutingContext(contextoDeAccion, [], new Dictionary<string, object?>(), new object());

        await filtro.OnActionExecutionAsync(ejecutando, () => Task.FromResult(
            new ActionExecutedContext(contextoDeAccion, [], new object()) { Result = resultado, Exception = excepcion }));
        return registro.Mensajes;
    }

    [Test]
    public async Task UnaModificacionExitosaDejaUnRegistroConAccionRecursoResultadoYUsuario()
    {
        var mensajes = await EjecutarAsync("PUT", new OkResult(), "ana@empresa.test");

        Assert.That(mensajes, Has.Count.EqualTo(1));
        Assert.That(mensajes[0], Does.Contain("PUT Actualizar").And.Contain("Clientes").And.Contain("recurso 7")
            .And.Contain("resultado 200").And.Contain("ana@empresa.test"));
    }

    [Test]
    public async Task SinSesionElUsuarioSeRegistraComoSinAutenticar()
    {
        var mensajes = await EjecutarAsync("POST", new CreatedResult("/x", null));

        Assert.That(mensajes[0], Does.Contain("resultado 201").And.Contain("sin autenticar"));
    }

    [Test]
    public async Task UnaAccionRechazadaTambienQuedaRegistradaConSuCodigo()
    {
        var mensajes = await EjecutarAsync("PATCH", new ConflictResult());

        Assert.That(mensajes[0], Does.Contain("resultado 409"));
    }

    [Test]
    public async Task UnaExcepcionSeRegistraComoError500()
    {
        var mensajes = await EjecutarAsync("DELETE", new EmptyResult(), excepcion: new InvalidOperationException("fallo"));

        Assert.That(mensajes[0], Does.Contain("resultado 500"));
    }

    [Test]
    public async Task UnaExcepcionDeNegocioSeRegistraConElCodigoQueRecibiraLaPersona()
    {
        var mensajes = await EjecutarAsync("PATCH", new EmptyResult(),
            excepcion: new ValidacionDeNegocioException("Estado inválido.", "ESTADO_INVALIDO"));

        Assert.That(mensajes[0], Does.Contain("resultado 400"));
    }

    [Test]
    public async Task UnSubrecursoRegistraElIdentificadorDeLaOrdenDeLaRuta()
    {
        var mensajes = await EjecutarAsync("POST", new CreatedResult("/x", null), idDelRecurso: null, idsDeRuta: ("ordenTrabajoId", "42"));

        Assert.That(mensajes[0], Does.Contain("recurso 42"));
    }

    [Test]
    public async Task UnaAltaRegistraElIdentificadorDelRecursoCreado()
    {
        var mensajes = await EjecutarAsync("POST", new CreatedResult("/x", new { Id = 99L }), idDelRecurso: null);

        Assert.That(mensajes[0], Does.Contain("recurso 99"));
    }

    [TestCase("GET")]
    [TestCase("HEAD")]
    [TestCase("OPTIONS")]
    public async Task LasLecturasNoSeAuditan(string metodo)
    {
        var mensajes = await EjecutarAsync(metodo, new OkResult());

        Assert.That(mensajes, Is.Empty);
    }

    [Test]
    public async Task ElRegistroNoIncluyeCuerposNiDatosPersonales()
    {
        var mensajes = await EjecutarAsync("POST", new CreatedResult("/x", new { contrasena = "Secreta123!", correo = "x@y.test" }));

        Assert.That(mensajes[0], Does.Not.Contain("Secreta123").And.Not.Contain("x@y.test"));
    }
}
