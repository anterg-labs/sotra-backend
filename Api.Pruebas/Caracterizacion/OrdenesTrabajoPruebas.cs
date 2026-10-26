using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class OrdenesTrabajoPruebas : PruebaBase
{
    private const long RolTecnico = 3;

    private async Task<long> CrearTecnicoAsync()
    {
        var tecnico = await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [RolTecnico]));
        return tecnico.GetProperty("id").GetInt64();
    }

    private async Task<JsonElement> CrearOrdenAsync(long? tecnicoId = null, long[]? repuestosIds = null)
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tecnicoId,
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Cambio de filtro hidráulico",
            observaciones = "",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            sugerencias = Array.Empty<string>(),
            repuestosIds = repuestosIds ?? [],
        });
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.LeerJsonAsync();
    }

    private Task<HttpResponseMessage> CambiarEstadoAsync(long id, string estado, string? motivo = null)
    {
        return Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/estado", new { estado, motivo });
    }

    private static async Task<string?> LeerCodigoAsync(HttpResponseMessage respuesta)
    {
        return (await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString();
    }

    private async Task<long> CrearOrdenAsignadaAsync()
    {
        var tecnicoId = await CrearTecnicoAsync();
        var orden = await CrearOrdenAsync(tecnicoId);
        var id = orden.GetProperty("id").GetInt64();
        (await CambiarEstadoAsync(id, "asignada")).EnsureSuccessStatusCode();
        return id;
    }

    [Test]
    public async Task UnaOrdenNuevaNaceEnEstadoCreadaConSusTransiciones()
    {
        var orden = await CrearOrdenAsync();

        Assert.That(orden.GetProperty("estado").GetString(), Is.EqualTo("creada"));
        var transiciones = orden.GetProperty("transiciones").EnumerateArray().ToList();
        Assert.That(transiciones.Select(t => t.GetProperty("estado").GetString()), Is.EqualTo(new[] { "asignada", "cancelada" }));
        Assert.That(transiciones.Select(t => t.GetProperty("exigeMotivo").GetBoolean()), Is.EqualTo(new[] { false, true }));
        Assert.That(transiciones.Select(t => t.GetProperty("puedeTecnico").GetBoolean()), Is.EqualTo(new[] { false, false }));
        Assert.That(orden.TryGetProperty("estadosSiguientes", out _), Is.False);
    }

    [Test]
    public async Task UnaOrdenSinDescripcionOConUnTipoDesconocidoResponde400()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var cliente = activo.GetProperty("clienteId").GetInt64();
        var activoId = activo.GetProperty("id").GetInt64();
        var fecha = DateOnly.FromDateTime(DateTime.UtcNow);

        var sinDescripcion = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = cliente,
            activoId,
            tipo = "correctivo",
            prioridad = "alta",
            descripcion = "",
            fechaPrevistaEjecucion = fecha,
        });
        var tipoInvalido = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = cliente,
            activoId,
            tipo = "otro",
            prioridad = "alta",
            descripcion = "Revisar",
            fechaPrevistaEjecucion = fecha,
        });

        Assert.That(sinDescripcion.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(tipoInvalido.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("descripcion", 4001, true)]
    [TestCase("observaciones", 4001, true)]
    [TestCase("descripcion", 4000, false)]
    [TestCase("observaciones", 4000, false)]
    public async Task LosTextosLibresDeLaOrdenTienenUnMaximoDe4000Caracteres(string campo, int largo, bool seRechaza)
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var texto = new string('a', largo);
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo = "correctivo",
            prioridad = "media",
            descripcion = campo == "descripcion" ? texto : "Revisión",
            observaciones = campo == "observaciones" ? texto : "",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(seRechaza ? HttpStatusCode.BadRequest : HttpStatusCode.Created));
        if (seRechaza)
        {
            var cuerpo = await respuesta.LeerJsonAsync();
            Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("VALIDACION_FALLIDA"));
            Assert.That(cuerpo.GetProperty("mensaje").GetString(), Does.Contain("4000 caracteres"));
        }
    }

    [Test]
    public async Task UnActivoDeOtroClienteSeRechaza()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var otroCliente = await DatosDePrueba.CrearClienteAsync(Cliente);

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = otroCliente.GetProperty("id").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo = "correctivo",
            prioridad = "baja",
            descripcion = "Revisión",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow),
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ElCicloCompletoCreadaAsignadaEnEjecucionFinalizadaCerradaRegistraElHistorial()
    {
        var id = await CrearOrdenAsignadaAsync();

        Assert.That((await CambiarEstadoAsync(id, "en_ejecucion")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await CambiarEstadoAsync(id, "finalizada")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var cierre = await CambiarEstadoAsync(id, "cerrada", "Trabajo verificado");

        Assert.That(cierre.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var orden = await cierre.LeerJsonAsync();
        Assert.That(orden.GetProperty("estado").GetString(), Is.EqualTo("cerrada"));
        Assert.That(orden.GetProperty("fechaInicio").ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
        Assert.That(orden.GetProperty("fechaCierre").ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
        Assert.That(orden.GetProperty("historialEstados").GetArrayLength(), Is.EqualTo(5));
    }

    [Test]
    public async Task UnSaltoDeEstadoInvalidoResponde400ConCodigoEstable()
    {
        var orden = await CrearOrdenAsync();

        var respuesta = await CambiarEstadoAsync(orden.GetProperty("id").GetInt64(), "finalizada");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("TRANSICION_INVALIDA"));
    }

    [Test]
    public async Task AsignarSinTecnicoResponde400()
    {
        var orden = await CrearOrdenAsync();

        var respuesta = await CambiarEstadoAsync(orden.GetProperty("id").GetInt64(), "asignada");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("TECNICO_OBLIGATORIO"));
    }

    [TestCase("pausada")]
    [TestCase("cancelada")]
    public async Task PausarOCancelarExigeMotivo(string estado)
    {
        var id = await CrearOrdenAsignadaAsync();
        if (estado == "pausada")
        {
            (await CambiarEstadoAsync(id, "en_ejecucion")).EnsureSuccessStatusCode();
        }

        var sinMotivo = await CambiarEstadoAsync(id, estado, "  ");
        var conMotivo = await CambiarEstadoAsync(id, estado, "Falta de material");

        Assert.That(sinMotivo.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(sinMotivo), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(conMotivo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task UnaOrdenCerradaYUnaCanceladaNoAdmitenMasCambios()
    {
        var cancelada = await CrearOrdenAsync();
        var idCancelada = cancelada.GetProperty("id").GetInt64();
        (await CambiarEstadoAsync(idCancelada, "cancelada", "Duplicada")).EnsureSuccessStatusCode();
        var cerrada = await CrearOrdenAsignadaAsync();
        foreach (var estado in new[] { "en_ejecucion", "finalizada" })
        {
            (await CambiarEstadoAsync(cerrada, estado)).EnsureSuccessStatusCode();
        }

        (await CambiarEstadoAsync(cerrada, "cerrada", "Verificada")).EnsureSuccessStatusCode();

        var sobreCerrada = await CambiarEstadoAsync(cerrada, "en_ejecucion");
        var sobreCancelada = await CambiarEstadoAsync(idCancelada, "asignada");
        var tecnicoEnCancelada = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{idCancelada}/tecnico",
            new { tecnicoId = await CrearTecnicoAsync() });

        Assert.That(sobreCerrada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(sobreCerrada), Is.EqualTo("ORDEN_CERRADA"));
        Assert.That(sobreCancelada.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(tecnicoEnCancelada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(tecnicoEnCancelada), Is.EqualTo("ORDEN_NO_MODIFICABLE"));
    }

    [Test]
    public async Task AsignarUnTecnicoPasaLaOrdenCreadaAAsignada()
    {
        var orden = await CrearOrdenAsync();
        var tecnicoId = await CrearTecnicoAsync();

        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{orden.GetProperty("id").GetInt64()}/tecnico",
            new { tecnicoId });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualizada = await respuesta.LeerJsonAsync();
        Assert.That(actualizada.GetProperty("estado").GetString(), Is.EqualTo("asignada"));
        Assert.That(actualizada.GetProperty("tecnicoId").GetInt64(), Is.EqualTo(tecnicoId));
    }

    [Test]
    public async Task UnUsuarioSinRolTecnicoNoSePuedeAsignar()
    {
        var orden = await CrearOrdenAsync();
        var administrador = await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [1]));

        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{orden.GetProperty("id").GetInt64()}/tecnico",
            new { tecnicoId = administrador.GetProperty("id").GetInt64() });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("creada")]
    [TestCase("asignada")]
    [TestCase("en_ejecucion")]
    public async Task RepetirElTecnicoActualResponde409SinCambiarLaOrdenNiElHistorial(string estado)
    {
        var tecnicoId = await CrearTecnicoAsync();
        var inicial = await CrearOrdenAsync(tecnicoId);
        var id = inicial.GetProperty("id").GetInt64();
        if (estado != "creada") (await CambiarEstadoAsync(id, "asignada")).EnsureSuccessStatusCode();
        if (estado == "en_ejecucion") (await CambiarEstadoAsync(id, estado)).EnsureSuccessStatusCode();
        var antes = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();

        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/tecnico", new { tecnicoId });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("TECNICO_YA_ASIGNADO"));
        var despues = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(despues.GetRawText(), Is.EqualTo(antes.GetRawText()));
    }

    [Test]
    public async Task SePuedeVolverAUnTecnicoAnteriorDespuesDeAsignarAOtro()
    {
        var primero = await CrearTecnicoAsync();
        var segundo = await CrearTecnicoAsync();
        var inicial = await CrearOrdenAsync();
        var id = inicial.GetProperty("id").GetInt64();
        var historialInicial = inicial.GetProperty("historialEstados").GetArrayLength();
        foreach (var tecnicoId in new[] { primero, segundo, primero })
        {
            var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/tecnico", new { tecnicoId });
            respuesta.EnsureSuccessStatusCode();
        }

        var final = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(final.GetProperty("tecnicoId").GetInt64(), Is.EqualTo(primero));
        Assert.That(final.GetProperty("historialEstados").GetArrayLength(), Is.EqualTo(historialInicial + 3));
    }

    [Test]
    public async Task AsignacionesSimultaneasDelMismoTecnicoSoloAgreganUnHistorial()
    {
        var tecnicoId = await CrearTecnicoAsync();
        var inicial = await CrearOrdenAsync();
        var id = inicial.GetProperty("id").GetInt64();
        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/tecnico", new { tecnicoId })));

        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict), Is.EqualTo(4));
        foreach (var rechazada in respuestas.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            Assert.That(await LeerCodigoAsync(rechazada), Is.EqualTo("TECNICO_YA_ASIGNADO"));
        var final = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(final.GetProperty("historialEstados").GetArrayLength(),
            Is.EqualTo(inicial.GetProperty("historialEstados").GetArrayLength() + 1));
    }

    [Test]
    public async Task UnaOrdenInexistenteResponde404()
    {
        Assert.That((await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{long.MaxValue}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await CambiarEstadoAsync(long.MaxValue, "asignada")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task LosConteosDeUsuariosYClientesCuentanSoloOrdenesActivasYReflejanLaReasignacion()
    {
        var primero = await CrearTecnicoAsync();
        var segundo = await CrearTecnicoAsync();
        var sinOrdenes = await DatosDePrueba.CrearClienteAsync(Cliente);
        var inicial = await CrearOrdenAsync(primero);
        var id = inicial.GetProperty("id").GetInt64();
        var clienteId = inicial.GetProperty("clienteId").GetInt64();
        var otra = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId,
            activoId = inicial.GetProperty("activoId").GetInt64(),
            tecnicoId = primero,
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Otra orden del mismo cliente",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))
        });
        otra.EnsureSuccessStatusCode();
        var otraOrden = await otra.LeerJsonAsync();
        (await CambiarEstadoAsync(otraOrden.GetProperty("id").GetInt64(), "cancelada", "Prueba de conteo histórico"))
            .EnsureSuccessStatusCode();

        async Task<int> Cantidad(string modulo, long recursoId, string campo)
            => (await (await Cliente.GetAsync($"/api/v1/{modulo}/{recursoId}")).LeerJsonAsync()).GetProperty(campo).GetInt32();

        // La orden cancelada no cuenta: solo las activas (no cerradas ni canceladas).
        Assert.That(await Cantidad("usuarios", primero, "cantidadOrdenesAsignadas"), Is.EqualTo(1));
        Assert.That(await Cantidad("usuarios", segundo, "cantidadOrdenesAsignadas"), Is.Zero);
        Assert.That(await Cantidad("clientes", clienteId, "cantidadOrdenesAsociadas"), Is.EqualTo(1));
        Assert.That(await Cantidad("clientes", sinOrdenes.GetProperty("id").GetInt64(), "cantidadOrdenesAsociadas"), Is.Zero);
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/tecnico", new { tecnicoId = segundo })).EnsureSuccessStatusCode();
        Assert.That(await Cantidad("usuarios", primero, "cantidadOrdenesAsignadas"), Is.Zero);
        Assert.That(await Cantidad("usuarios", segundo, "cantidadOrdenesAsignadas"), Is.EqualTo(1));
        Assert.That(await Cantidad("clientes", clienteId, "cantidadOrdenesAsociadas"), Is.EqualTo(1));

        foreach (var (modulo, campo) in new[] { ("usuarios", "cantidadOrdenesAsignadas"), ("clientes", "cantidadOrdenesAsociadas") })
        {
            foreach (var direccion in new[] { "asc", "desc" })
            {
                var pagina = await (await Cliente.GetAsync($"/api/v1/{modulo}?ordenarPor={campo}&direccionOrden={direccion}&tamanoPagina=3")).LeerJsonAsync();
                var elementos = pagina.GetProperty("elementos").EnumerateArray().ToArray();
                var cantidades = elementos.Select(e => e.GetProperty(campo).GetInt32()).ToArray();
                Assert.That(cantidades, direccion == "asc" ? Is.Ordered.Ascending : Is.Ordered.Descending);
                foreach (var elemento in elementos)
                    Assert.That(elemento.GetProperty(campo).GetInt32(), Is.EqualTo(await Cantidad(modulo, elemento.GetProperty("id").GetInt64(), campo)));
            }
        }
    }

    [Test]
    public async Task CincoCambiosDeEstadoSimultaneosAlMismoDestinoSoloSeAplicanUnaVez()
    {
        var id = await CrearOrdenAsignadaAsync();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => CambiarEstadoAsync(id, "en_ejecucion")));

        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.BadRequest), Is.EqualTo(4));
        var orden = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        var enEjecucion = orden.GetProperty("historialEstados").EnumerateArray()
            .Count(h => h.GetProperty("estado").GetString() == "en_ejecucion");
        Assert.That(enEjecucion, Is.EqualTo(1));
    }

    [Test]
    public async Task ElListadoPaginadoYFiltradoPorEstadoDevuelveSoloEsasOrdenes()
    {
        var id = await CrearOrdenAsignadaAsync();

        var pagina = await (await Cliente.GetAsync("/api/v1/ordenes-trabajo?estado=asignada&tamanoPagina=100")).LeerJsonAsync();

        var estados = pagina.GetProperty("elementos").EnumerateArray().Select(o => o.GetProperty("estado").GetString()).Distinct();
        Assert.That(estados, Is.EqualTo(new[] { "asignada" }));
        var ids = pagina.GetProperty("elementos").EnumerateArray().Select(o => o.GetProperty("id").GetInt64());
        Assert.That(ids, Does.Contain(id));
    }

    private async Task<HttpResponseMessage> ModificarAsync(long id, JsonElement orden, string estado, string descripcion,
        string? motivoEstado = null, string? ifMatch = null)
    {
        using var solicitud = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/ordenes-trabajo/{id}");
        if (ifMatch is not null)
        {
            solicitud.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        solicitud.Content = JsonContent.Create(new
        {
            clienteId = orden.GetProperty("clienteId").GetInt64(),
            activoId = orden.GetProperty("activoId").GetInt64(),
            tecnicoId = orden.GetProperty("tecnicoId").GetInt64(),
            tipo = "correctivo",
            prioridad = "alta",
            descripcion,
            observaciones = "",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5)),
            sugerencias = Array.Empty<string>(),
            repuestosIds = Array.Empty<long>(),
            estado,
            motivoEstado,
        });
        return await Cliente.SendAsync(solicitud);
    }

    private async Task<(long Id, JsonElement Orden)> CrearOrdenAsignadaCompletaAsync()
    {
        var id = await CrearOrdenAsignadaAsync();
        var orden = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        return (id, orden);
    }

    [Test]
    public async Task LaLecturaYLaModificacionDevuelvenLaVersionComoETag()
    {
        var (id, orden) = await CrearOrdenAsignadaCompletaAsync();

        var lectura = await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}");
        var version = (await lectura.LeerJsonAsync()).GetProperty("version").GetString();

        Assert.That(version, Is.Not.Empty);
        Assert.That(lectura.Headers.ETag?.Tag, Is.EqualTo($"\"{version}\""));

        var modificada = await ModificarAsync(id, orden, "asignada", "Con versión nueva", ifMatch: $"\"{version}\"");
        Assert.That(modificada.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var versionNueva = (await modificada.LeerJsonAsync()).GetProperty("version").GetString();
        Assert.That(versionNueva, Is.Not.EqualTo(version));
        Assert.That(modificada.Headers.ETag?.Tag, Is.EqualTo($"\"{versionNueva}\""));
    }

    [Test]
    public async Task ModificarConUnaVersionVencidaResponde412YNoCambiaNada()
    {
        var (id, orden) = await CrearOrdenAsignadaCompletaAsync();
        var version = orden.GetProperty("version").GetString();
        (await ModificarAsync(id, orden, "asignada", "Primera edición", ifMatch: $"\"{version}\"")).EnsureSuccessStatusCode();

        var tardia = await ModificarAsync(id, orden, "asignada", "Segunda edición con datos viejos", ifMatch: $"\"{version}\"");

        Assert.That(tardia.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
        Assert.That(await LeerCodigoAsync(tardia), Is.EqualTo("ORDEN_MODIFICADA"));
        var actual = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(actual.GetProperty("descripcion").GetString(), Is.EqualTo("Primera edición"));
    }

    [Test]
    public async Task ModificarSinIfMatchOConAsteriscoMantieneElComportamientoAnterior()
    {
        var (id, orden) = await CrearOrdenAsignadaCompletaAsync();

        var sinEncabezado = await ModificarAsync(id, orden, "asignada", "Sin encabezado");
        var conAsterisco = await ModificarAsync(id, orden, "asignada", "Con asterisco", ifMatch: "*");

        Assert.That(sinEncabezado.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(conAsterisco.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DosEdicionesSimultaneasConLaMismaVersionSoloAplicanUna()
    {
        var (id, orden) = await CrearOrdenAsignadaCompletaAsync();
        var version = orden.GetProperty("version").GetString();

        var respuestas = await Task.WhenAll(Enumerable.Range(1, 5)
            .Select(n => ModificarAsync(id, orden, "asignada", $"Edición {n}", ifMatch: $"\"{version}\"")));

        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.PreconditionFailed), Is.EqualTo(4));
    }

    [Test]
    public async Task ModificarUnaOrdenCambiaSusDatosYPuedeAvanzarElEstado()
    {
        var (id, orden) = await CrearOrdenAsignadaCompletaAsync();

        var respuesta = await ModificarAsync(id, orden, "en_ejecucion", "Descripción corregida");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualizada = await respuesta.LeerJsonAsync();
        Assert.That(actualizada.GetProperty("descripcion").GetString(), Is.EqualTo("Descripción corregida"));
        Assert.That(actualizada.GetProperty("prioridad").GetString(), Is.EqualTo("alta"));
        Assert.That(actualizada.GetProperty("estado").GetString(), Is.EqualTo("en_ejecucion"));
    }

    [Test]
    public async Task ModificarUnaOrdenInexistenteResponde404YUnaCerradaResponde409()
    {
        var (id, orden) = await CrearOrdenAsignadaCompletaAsync();
        foreach (var estado in new[] { "en_ejecucion", "finalizada" })
        {
            (await CambiarEstadoAsync(id, estado)).EnsureSuccessStatusCode();
        }

        (await CambiarEstadoAsync(id, "cerrada", "Verificada")).EnsureSuccessStatusCode();

        var inexistente = await ModificarAsync(long.MaxValue, orden, "asignada", "x");
        var cerrada = await ModificarAsync(id, orden, "cerrada", "Cambio tardío");

        Assert.That(inexistente.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(cerrada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(cerrada), Is.EqualTo("ORDEN_CERRADA"));
    }

    [Test]
    public async Task UnaTransicionInvalidaPorPutResponde400YNoCambiaNada()
    {
        var (id, orden) = await CrearOrdenAsignadaCompletaAsync();

        var respuesta = await ModificarAsync(id, orden, "cerrada", "Salto inválido", "Motivo");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("TRANSICION_INVALIDA"));
        var sinCambios = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(sinCambios.GetProperty("estado").GetString(), Is.EqualTo("asignada"));
        Assert.That(sinCambios.GetProperty("descripcion").GetString(), Is.Not.EqualTo("Salto inválido"));
    }

    [Test]
    public async Task CincoModificacionesSimultaneasAlMismoEstadoSoloRegistranUnaVezElCambio()
    {
        var (id, orden) = await CrearOrdenAsignadaCompletaAsync();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(i => ModificarAsync(id, orden, "en_ejecucion", "Edición " + i)));

        Assert.That(respuestas.Select(r => r.StatusCode), Has.All.EqualTo(HttpStatusCode.OK));
        var final = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        var enEjecucion = final.GetProperty("historialEstados").EnumerateArray()
            .Count(h => h.GetProperty("estado").GetString() == "en_ejecucion");
        Assert.That(enEjecucion, Is.EqualTo(1));
    }

    [Test]
    public async Task ListarEstadosDevuelveElCatalogoDeLaBaseEnElOrdenDelCiclo()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/ordenes-trabajo/estados");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var estados = await respuesta.LeerJsonAsync();
        var valores = estados.EnumerateArray().Select(estado => estado.GetProperty("valor").GetString()).ToList();
        Assert.That(valores, Is.EqualTo(new[] { "creada", "asignada", "en_ejecucion", "pausada", "finalizada", "cerrada", "cancelada" }));
        var etiquetas = estados.EnumerateArray().Select(estado => estado.GetProperty("etiqueta").GetString()).ToList();
        Assert.That(etiquetas, Is.EqualTo(new[] { "Creada", "Asignada", "En ejecución", "Pausada", "Finalizada", "Cerrada", "Cancelada" }));
        Assert.That(estados.EnumerateArray().Select(estado => estado.GetProperty("orden").GetInt32()), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7 }));
        Assert.That(estados.EnumerateArray().Select(estado => estado.GetProperty("fase").GetString()),
            Is.EqualTo(new[] { "pendiente", "pendiente", "en_curso", "en_curso", "por_validar", "cerrada", "cancelada" }));
        Assert.That(estados.EnumerateArray().Select(estado => estado.GetProperty("tono").GetString()),
            Is.EqualTo(new[] { "neutro", "info", "info", "alerta", "destacado", "exito", "neutro" }));
    }

    [Test]
    public async Task LaBaseTieneLasReglasDeEstadoDeLaSeccion13Punto1DelDocumento()
    {
        using var alcance = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(EntornoDePruebas.Fabrica.Services);
        var reglas = await Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SOTRA_BACKEND.Operaciones.OrdenTrabajoDAO>(alcance.ServiceProvider)
            .ObtenerReglasAsync(CancellationToken.None);

        static string Describir(SOTRA_BACKEND.Operaciones.EstadoDeOrden e) =>
            $"{e.Orden}|{e.Nombre}|{e.Etiqueta}|{e.Fase}|{e.ExigeTecnico}|{e.CompletaFecha}|{e.Tono}|{e.CodigoAlerta}|{e.EsInicial}";
        Assert.That(reglas.Estados.Select(Describir), Is.EqualTo(Api.PruebasUnitarias.ReglasDePrueba.EstadosDocumentados.Select(Describir)));

        var enLaBase = reglas.Estados.SelectMany(e => reglas.TransicionesDesde(e.Nombre)).ToHashSet();
        Assert.That(enLaBase, Is.EquivalentTo(Api.PruebasUnitarias.ReglasDePrueba.TransicionesDocumentadas));
    }
}
