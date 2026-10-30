using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using Api.DTOs;
using Api.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

public class InfraestructuraPruebas
{
    private static readonly JsonSerializerOptions OpcionesSinNulos = new()
    {
        Converters = { new TextoSinCaracteresNulosConverter() },
    };

    private sealed record Carga(string? Texto);

    private sealed class ServicioDeProblemasFalso : IProblemDetailsService
    {
        public ProblemDetailsContext? Recibido { get; private set; }

        public ValueTask WriteAsync(ProblemDetailsContext contexto)
        {
            Recibido = contexto;
            return ValueTask.CompletedTask;
        }

        public ValueTask<bool> TryWriteAsync(ProblemDetailsContext contexto)
        {
            Recibido = contexto;
            return ValueTask.FromResult(true);
        }
    }

    private static (DefaultHttpContext Contexto, ServicioDeProblemasFalso Servicio) CrearContexto(
        string ruta = "/api/v1/clientes", string consulta = "")
    {
        var servicio = new ServicioDeProblemasFalso();
        var proveedor = new ServiceCollection().AddSingleton<IProblemDetailsService>(servicio).BuildServiceProvider();
        var contexto = new DefaultHttpContext { RequestServices = proveedor };
        contexto.Request.Path = ruta;
        contexto.Request.QueryString = new QueryString(consulta);
        contexto.Response.Body = new MemoryStream();
        return (contexto, servicio);
    }

    private static async Task<bool> EjecutarMiddlewareAsync(DefaultHttpContext contexto)
    {
        var siguienteEjecutado = false;
        var middleware = new EntradaSinCaracteresNulosMiddleware(_ =>
        {
            siguienteEjecutado = true;
            return Task.CompletedTask;
        });
        await middleware.InvokeAsync(contexto);
        return siguienteEjecutado;
    }

    [Test]
    public void Convertidor_DeserializaTextoNormal()
    {
        var carga = JsonSerializer.Deserialize<Carga>("""{"Texto":"hola"}""", OpcionesSinNulos);

        Assert.That(carga!.Texto, Is.EqualTo("hola"));
    }

    [Test]
    public void Convertidor_RechazaCaracterNulo()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Carga>("{\"Texto\":\"ho\\u0000la\"}", OpcionesSinNulos));
    }

    [Test]
    public void Convertidor_DejaPasarNuloJson()
    {
        var carga = JsonSerializer.Deserialize<Carga>("""{"Texto":null}""", OpcionesSinNulos);

        Assert.That(carga!.Texto, Is.Null);
    }

    [Test]
    public void Convertidor_SerializaIgualQueElPredeterminado()
    {
        Assert.That(JsonSerializer.Serialize(new Carga("á\"b"), OpcionesSinNulos),
            Is.EqualTo(JsonSerializer.Serialize(new Carga("á\"b"))));
    }

    [Test]
    public async Task Middleware_DejaPasarSolicitudLimpia()
    {
        var (contexto, _) = CrearContexto(consulta: "?busqueda=abc");

        Assert.That(await EjecutarMiddlewareAsync(contexto), Is.True);
        Assert.That(contexto.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
    }

    [TestCase("/api/v1/clientes/a\0b", "")]
    [TestCase("/api/v1/clientes", "?busqueda=a%00b")]
    [TestCase("/api/v1/clientes", "?busqueda=a\0b")]
    public async Task Middleware_RechazaNuloEnRutaOConsulta(string ruta, string consulta)
    {
        var (contexto, servicio) = CrearContexto(ruta, consulta);

        Assert.That(await EjecutarMiddlewareAsync(contexto), Is.False);
        Assert.That(contexto.Response.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(servicio.Recibido!.ProblemDetails.Extensions["codigo"], Is.EqualTo("ENTRADA_INVALIDA"));
        Assert.That(servicio.Recibido.ProblemDetails.Status, Is.EqualTo(400));
    }

    [Test]
    public async Task Middleware_RechazaNuloEnCampoDeFormulario()
    {
        var (contexto, _) = CrearContexto();
        contexto.Request.ContentType = "application/x-www-form-urlencoded";
        contexto.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("nombre=a%00b"));

        Assert.That(await EjecutarMiddlewareAsync(contexto), Is.False);
        Assert.That(contexto.Response.StatusCode, Is.EqualTo(400));
    }

    [Test]
    public async Task Middleware_DejaPasarFormularioLimpio()
    {
        var (contexto, _) = CrearContexto();
        contexto.Request.ContentType = "application/x-www-form-urlencoded";
        contexto.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("nombre=Ana"));

        Assert.That(await EjecutarMiddlewareAsync(contexto), Is.True);
    }

    [Test]
    public async Task Middleware_NoLeeCuerpoJson()
    {
        var (contexto, _) = CrearContexto();
        contexto.Request.ContentType = "application/json";
        contexto.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"a\":\"b\"}"));

        Assert.That(await EjecutarMiddlewareAsync(contexto), Is.True);
        Assert.That(contexto.Request.Body.Position, Is.Zero);
    }

    private sealed class RespuestaConInicioManual : IHttpResponseFeature
    {
        private readonly List<(Func<object, Task> Accion, object Estado)> _alIniciar = [];

        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();
        public bool HasStarted => false;

        public void OnStarting(Func<object, Task> callback, object state) => _alIniciar.Add((callback, state));

        public void OnCompleted(Func<object, Task> callback, object state)
        {
        }

        public async Task IniciarAsync()
        {
            foreach (var (accion, estado) in _alIniciar)
            {
                await accion(estado);
            }
        }
    }

    private static async Task<IHeaderDictionary> EjecutarCabecerasAsync(
        string ruta, Action<IHeaderDictionary>? preparar = null)
    {
        var contexto = new DefaultHttpContext();
        var respuesta = new RespuestaConInicioManual();
        contexto.Features.Set<IHttpResponseFeature>(respuesta);
        contexto.Request.Path = ruta;
        preparar?.Invoke(respuesta.Headers);

        await new CabecerasDeSeguridadMiddleware(_ => Task.CompletedTask).InvokeAsync(contexto);
        await respuesta.IniciarAsync();
        return respuesta.Headers;
    }

    [Test]
    public async Task Cabeceras_AgreganTodasLasCabecerasDeSeguridad()
    {
        var cabeceras = await EjecutarCabecerasAsync("/api/v1/clientes");

        Assert.Multiple(() =>
        {
            Assert.That(cabeceras["X-Content-Type-Options"].ToString(), Is.EqualTo("nosniff"));
            Assert.That(cabeceras["X-Frame-Options"].ToString(), Is.EqualTo("DENY"));
            Assert.That(cabeceras["Referrer-Policy"].ToString(), Is.EqualTo("no-referrer"));
            Assert.That(cabeceras["Content-Security-Policy"].ToString(), Does.Contain("default-src 'none'"));
            Assert.That(cabeceras["Permissions-Policy"].ToString(), Does.Contain("camera=()"));
            Assert.That(cabeceras["Cross-Origin-Resource-Policy"].ToString(), Is.EqualTo("same-site"));
            Assert.That(cabeceras["Cache-Control"].ToString(), Is.EqualTo("no-store"));
        });
    }

    [Test]
    public async Task Cabeceras_NoPonenNoStoreFueraDeApi()
    {
        var cabeceras = await EjecutarCabecerasAsync("/salud");

        Assert.That(cabeceras.ContainsKey("Cache-Control"), Is.False);
        Assert.That(cabeceras["X-Frame-Options"].ToString(), Is.EqualTo("DENY"));
    }

    [Test]
    public async Task Cabeceras_RespetanCacheControlExistente()
    {
        var cabeceras = await EjecutarCabecerasAsync("/api/v1/version",
            existentes => existentes["Cache-Control"] = "public, max-age=60");

        Assert.That(cabeceras["Cache-Control"].ToString(), Is.EqualTo("public, max-age=60"));
    }

    private static List<ValidationResult> ValidarObjeto(object objeto)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(objeto, new ValidationContext(objeto), resultados, validateAllProperties: true);
        return resultados;
    }

    [Test]
    public void LimiteDeSolicitudes_ValoresPorDefectoSonValidos()
    {
        var opciones = new OpcionesDeLimiteDeSolicitudes();

        Assert.That(ValidarObjeto(opciones), Is.Empty);
        Assert.That(opciones.Habilitado, Is.True);
        Assert.That(opciones.PorMinutoSugerencias, Is.LessThan(opciones.PorMinutoGeneral));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(100_001)]
    public void LimiteDeSolicitudes_RechazaValoresFueraDeRango(int valor)
    {
        Assert.That(ValidarObjeto(new OpcionesDeLimiteDeSolicitudes { PorMinutoGeneral = valor }), Has.Count.EqualTo(1));
        Assert.That(ValidarObjeto(new OpcionesDeLimiteDeSolicitudes { PorMinutoSugerencias = valor }), Has.Count.EqualTo(1));
        Assert.That(ValidarObjeto(new OpcionesDeLimiteDeSolicitudes { PorMinutoSubidas = valor }), Has.Count.EqualTo(1));
        Assert.That(ValidarObjeto(new OpcionesDeLimiteDeSolicitudes { PorMinutoEscrituras = valor }), Has.Count.EqualTo(1));
    }

    [TestCase(1)]
    [TestCase(100_000)]
    public void LimiteDeSolicitudes_AceptaLosExtremosDelRango(int valor)
    {
        Assert.That(ValidarObjeto(new OpcionesDeLimiteDeSolicitudes { PorMinutoGeneral = valor }), Is.Empty);
    }

    [Test]
    public void LimiteDeSolicitudes_SeccionYPoliticasTienenNombresEstables()
    {
        Assert.That(OpcionesDeLimiteDeSolicitudes.Seccion, Is.EqualTo("LimiteSolicitudes"));
        Assert.That(PoliticasDeLimite.Sugerencias, Is.EqualTo("sugerencias"));
        Assert.That(PoliticasDeLimite.Subidas, Is.EqualTo("subidas"));
    }

    [Test]
    public void AlertasDeMaquinaria_ValoresPorDefectoSonValidos()
    {
        Assert.That(ValidarObjeto(new OpcionesDeAlertasDeMaquinaria()), Is.Empty);
    }

    [Test]
    public void AlertasDeMaquinaria_LasHorasAceptanCeroPeroNoNegativos()
    {
        Assert.That(ValidarObjeto(new OpcionesDeAlertasDeMaquinaria { SinAsignarHoras = 0 }), Is.Empty);
        Assert.That(ValidarObjeto(new OpcionesDeAlertasDeMaquinaria { SinAsignarHoras = -1 }), Has.Count.EqualTo(1));
        Assert.That(ValidarObjeto(new OpcionesDeAlertasDeMaquinaria { AsignadaSinIniciarHoras = -1 }), Has.Count.EqualTo(1));
        Assert.That(ValidarObjeto(new OpcionesDeAlertasDeMaquinaria { PausadaHoras = 100_001 }), Has.Count.EqualTo(1));
        Assert.That(ValidarObjeto(new OpcionesDeAlertasDeMaquinaria { MaquinaDetenidaAltaHoras = -5 }), Has.Count.EqualTo(1));
        Assert.That(ValidarObjeto(new OpcionesDeAlertasDeMaquinaria { MaquinaDetenidaMediaHoras = -5 }), Has.Count.EqualTo(1));
        Assert.That(ValidarObjeto(new OpcionesDeAlertasDeMaquinaria { MaquinaDetenidaBajaHoras = -5 }), Has.Count.EqualTo(1));
    }

    [TestCase(0)]
    [TestCase(3651)]
    public void AlertasDeMaquinaria_ReincidenciaExigeEntreUnoYTresMilSeiscientosCincuenta(int dias)
    {
        Assert.That(ValidarObjeto(new OpcionesDeAlertasDeMaquinaria { ReincidenciaDias = dias }), Has.Count.EqualTo(1));
    }

    [Test]
    public void AlertasDeMaquinaria_LosUmbralesDeDetencionCrecenConLaBajaPrioridad()
    {
        var opciones = new OpcionesDeAlertasDeMaquinaria();

        Assert.That(opciones.MaquinaDetenidaAltaHoras, Is.LessThan(opciones.MaquinaDetenidaMediaHoras));
        Assert.That(opciones.MaquinaDetenidaMediaHoras, Is.LessThan(opciones.MaquinaDetenidaBajaHoras));
    }

    [TestCase(1, 1, true)]
    [TestCase(1, 100, true)]
    [TestCase(5, 20, true)]
    [TestCase(0, 20, false)]
    [TestCase(-1, 20, false)]
    [TestCase(1, 0, false)]
    [TestCase(1, 101, false)]
    [TestCase(1, -5, false)]
    public void PaginaDto_EsValida_CompruebaLosLimites(int pagina, int tamano, bool esperado)
    {
        Assert.That(PaginaDto<int>.EsValida(pagina, tamano), Is.EqualTo(esperado));
    }

    [Test]
    public void PaginaDto_MensajeDePaginacionIndicaElMaximoPermitido()
    {
        Assert.That(PaginaDto<int>.MensajeDePaginacion(), Does.Contain("100"));
    }

    [TestCase(0, 20, 0)]
    [TestCase(1, 20, 1)]
    [TestCase(20, 20, 1)]
    [TestCase(21, 20, 2)]
    [TestCase(100, 1, 100)]
    public void PaginaDto_Crear_CalculaTotalDePaginas(int total, int tamano, int paginasEsperadas)
    {
        var pagina = PaginaDto<int>.Crear([], total, 1, tamano);

        Assert.That(pagina.TotalPaginas, Is.EqualTo(paginasEsperadas));
        Assert.That(pagina.TotalElementos, Is.EqualTo(total));
        Assert.That(pagina.TamanoPagina, Is.EqualTo(tamano));
    }

    [Test]
    public void PaginaDto_CrearDesdeLista_DevuelveLaPaginaPedida()
    {
        var todos = Enumerable.Range(1, 45).ToList();

        var segunda = PaginaDto<int>.Crear(todos, 2, 20);
        var tercera = PaginaDto<int>.Crear(todos, 3, 20);

        Assert.That(segunda.Elementos, Is.EqualTo(Enumerable.Range(21, 20)));
        Assert.That(tercera.Elementos, Is.EqualTo(Enumerable.Range(41, 5)));
        Assert.That(tercera.TotalElementos, Is.EqualTo(45));
        Assert.That(tercera.TotalPaginas, Is.EqualTo(3));
    }

    [Test]
    public void PaginaDto_CrearDesdeLista_PaginaFueraDeRangoQuedaVacia()
    {
        var pagina = PaginaDto<int>.Crear([1, 2, 3], 9, 20);

        Assert.That(pagina.Elementos, Is.Empty);
        Assert.That(pagina.TotalElementos, Is.EqualTo(3));
        Assert.That(pagina.TotalPaginas, Is.EqualTo(1));
    }

    [Test]
    public void PaginaDto_CrearDesdeListaVacia()
    {
        var pagina = PaginaDto<int>.Crear([], 1, 20);

        Assert.That(pagina.Elementos, Is.Empty);
        Assert.That(pagina.TotalPaginas, Is.Zero);
    }

    [Test]
    public void PaginaDto_Contiene_ComparaEnMinusculasYToleraNulo()
    {
        Assert.That(PaginaDto<int>.Contiene("Bomba HIDRÁULICA", "bomba"), Is.True);
        Assert.That(PaginaDto<int>.Contiene("Bomba", "motor"), Is.False);
        Assert.That(PaginaDto<int>.Contiene(null, "x"), Is.False);
    }

    [TestCase(0, null, "sin_stock")]
    [TestCase(-3, 5, "sin_stock")]
    [TestCase(0, 5, "sin_stock")]
    [TestCase(1, null, "normal")]
    [TestCase(1, 0, "normal")]
    [TestCase(5, 5, "bajo")]
    [TestCase(4, 5, "bajo")]
    [TestCase(6, 5, "normal")]
    [TestCase(1, -2, "normal")]
    [TestCase(int.MaxValue, int.MaxValue, "bajo")]
    public void EstadosStock_Calcular(int cantidad, int? minimo, string esperado)
    {
        Assert.That(EstadosStock.Calcular(cantidad, minimo), Is.EqualTo(esperado));
    }

    [Test]
    public void EstadosStock_ValidosContieneLosTresEstados()
    {
        Assert.That(EstadosStock.Validos, Is.EquivalentTo(new[] { "normal", "bajo", "sin_stock" }));
    }

    [Test]
    public void AlertasStock_TotalSumaBajoYSinStock()
    {
        Assert.That(new AlertasStock(3, 4).Total, Is.EqualTo(7));
        Assert.That(new AlertasStock(0, 0).Total, Is.Zero);
    }
}
