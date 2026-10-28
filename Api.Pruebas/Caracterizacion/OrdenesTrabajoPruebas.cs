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

    private async Task<JsonElement> CrearOrdenAsync(long? tecnicoId = null, long[]? repuestosIds = null, string descripcion = "Cambio de filtro hidráulico")
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tecnicoId,
            tipo = "correctivo",
            prioridad = "media",
            descripcion,
            observaciones = "",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)),
            sugerencias = Array.Empty<string>(),
            repuestos = (repuestosIds ?? []).Select(id => new { repuestoId = id, cantidad = 1 }),
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

    private async Task<long> CrearOrdenConTecnicoAsync()
    {
        var tecnicoId = await CrearTecnicoAsync();
        var orden = await CrearOrdenAsync(tecnicoId);
        return orden.GetProperty("id").GetInt64();
    }

    [Test]
    public async Task SinAsignarCuentaYListaSoloOrdenesPendientesSinTecnico()
    {
        async Task<int> ContarIndicadorAsync()
        {
            var respuesta = await (await Cliente.GetAsync("/api/v1/reportes/indicadores")).LeerJsonAsync();
            return respuesta.GetProperty("ordenesSinAsignar").GetInt32();
        }

        var cantidadInicial = await ContarIndicadorAsync();
        var tecnicoId = await CrearTecnicoAsync();
        var busqueda = $"Orden sin asignar {Guid.NewGuid():N}";
        var sinTecnico = await CrearOrdenAsync(descripcion: busqueda);
        var conTecnico = await CrearOrdenAsync(tecnicoId, descripcion: $"Orden asignada {Guid.NewGuid():N}");
        var idSinTecnico = sinTecnico.GetProperty("id").GetInt64();
        var idConTecnico = conTecnico.GetProperty("id").GetInt64();

        Assert.That(await ContarIndicadorAsync(), Is.EqualTo(cantidadInicial + 1));
        var listado = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo?sinAsignar=true&buscar={Uri.EscapeDataString(busqueda)}")).LeerJsonAsync();
        var ids = listado.GetProperty("elementos").EnumerateArray().Select(o => o.GetProperty("id").GetInt64()).ToList();
        Assert.That(ids, Does.Contain(idSinTecnico));
        Assert.That(listado.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
        var listadoAsignado = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo?sinAsignar=true&buscar={idConTecnico}")).LeerJsonAsync();
        Assert.That(listadoAsignado.GetProperty("elementos").EnumerateArray().Any(o => o.GetProperty("id").GetInt64() == idConTecnico), Is.False);

        var fecha = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(3)).ToString("yyyy-MM-dd");
        var calendario = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/calendario?desde={fecha}&hasta={fecha}&sinAsignar=true")).LeerJsonAsync();
        Assert.That(calendario.GetProperty("elementos").EnumerateArray().Any(o => o.GetProperty("id").GetInt64() == idSinTecnico), Is.True);
        Assert.That(calendario.GetProperty("elementos").EnumerateArray().Any(o => o.GetProperty("id").GetInt64() == idConTecnico), Is.False);

        var asignacion = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{idSinTecnico}/tecnico", new { tecnicoId });
        Assert.That(asignacion.StatusCode, Is.EqualTo(HttpStatusCode.OK), await asignacion.Content.ReadAsStringAsync());
        Assert.That(await ContarIndicadorAsync(), Is.EqualTo(cantidadInicial));
    }

    [Test]
    public async Task UnaOrdenNuevaNaceEnEstadoPendienteConSusTransiciones()
    {
        var orden = await CrearOrdenAsync();

        Assert.That(orden.GetProperty("estado").GetString(), Is.EqualTo("pendiente"));
        var transiciones = orden.GetProperty("transiciones").EnumerateArray().ToList();
        Assert.That(transiciones.Select(t => t.GetProperty("estado").GetString()), Is.EqualTo(new[] { "en_progreso", "eliminado" }));
        Assert.That(transiciones.Select(t => t.GetProperty("exigeMotivo").GetBoolean()), Is.EqualTo(new[] { false, true }));
        Assert.That(transiciones.Select(t => t.GetProperty("puedeTecnico").GetBoolean()), Is.EqualTo(new[] { true, false }));
        Assert.That(orden.TryGetProperty("estadosSiguientes", out _), Is.False);
    }

    [Test]
    public async Task LaOrdenGuardaYMuestraQuienLaCreo()
    {
        var orden = await CrearOrdenAsync();

        var detalle = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{orden.GetProperty("id").GetInt64()}")).LeerJsonAsync();

        Assert.That(detalle.GetProperty("responsableId").GetInt64(), Is.GreaterThan(0));
        Assert.That(detalle.GetProperty("nombreResponsable").GetString(), Is.Not.Empty);
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
    public async Task ElCicloCompletoPendienteEnProgresoEnRevisionCerradoRegistraElHistorial()
    {
        var id = await CrearOrdenConTecnicoAsync();

        Assert.That((await CambiarEstadoAsync(id, "en_progreso")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await CambiarEstadoAsync(id, "en_revision")).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var cierre = await CambiarEstadoAsync(id, "cerrado");

        Assert.That(cierre.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var orden = await cierre.LeerJsonAsync();
        Assert.That(orden.GetProperty("estado").GetString(), Is.EqualTo("cerrado"));
        Assert.That(orden.GetProperty("fechaInicio").ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
        Assert.That(orden.GetProperty("fechaCierre").ValueKind, Is.Not.EqualTo(JsonValueKind.Null));
        var historial = orden.GetProperty("historialEstados").EnumerateArray().ToList();
        Assert.That(historial.Select(h => h.GetProperty("estado").GetString()), Is.EqualTo(new[] { "pendiente", "en_progreso", "en_revision", "cerrado" }));
        Assert.That(historial.Select(h => h.GetProperty("estadoAnterior").GetString()), Is.EqualTo(new[] { null, "pendiente", "en_progreso", "en_revision" }));
    }

    [Test]
    public async Task UnSaltoDeEstadoInvalidoResponde400ConCodigoEstable()
    {
        var orden = await CrearOrdenAsync();

        var respuesta = await CambiarEstadoAsync(orden.GetProperty("id").GetInt64(), "en_revision");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("TRANSICION_INVALIDA"));
    }

    [Test]
    public async Task IniciarSinTecnicoResponde400()
    {
        var orden = await CrearOrdenAsync();

        var respuesta = await CambiarEstadoAsync(orden.GetProperty("id").GetInt64(), "en_progreso");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("TECNICO_OBLIGATORIO"));
    }

    [TestCase("pausado")]
    [TestCase("eliminado")]
    public async Task PausarOEliminarExigeMotivo(string estado)
    {
        var id = await CrearOrdenConTecnicoAsync();
        if (estado == "pausado")
        {
            (await CambiarEstadoAsync(id, "en_progreso")).EnsureSuccessStatusCode();
        }

        var sinMotivo = await CambiarEstadoAsync(id, estado, "  ");
        var conMotivo = await CambiarEstadoAsync(id, estado, "Falta de material");

        Assert.That(sinMotivo.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(sinMotivo), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(conMotivo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task UnaOrdenCerradaYUnaEliminadaNoAdmitenMasCambios()
    {
        var eliminada = await CrearOrdenAsync();
        var idEliminada = eliminada.GetProperty("id").GetInt64();
        (await CambiarEstadoAsync(idEliminada, "eliminado", "Duplicada")).EnsureSuccessStatusCode();
        var cerrada = await CrearOrdenConTecnicoAsync();
        foreach (var estado in new[] { "en_progreso", "en_revision" })
        {
            (await CambiarEstadoAsync(cerrada, estado)).EnsureSuccessStatusCode();
        }

        (await CambiarEstadoAsync(cerrada, "cerrado")).EnsureSuccessStatusCode();

        var sobreCerrada = await CambiarEstadoAsync(cerrada, "en_progreso");
        var sobreEliminada = await CambiarEstadoAsync(idEliminada, "pendiente");
        var tecnicoEnEliminada = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{idEliminada}/tecnico",
            new { tecnicoId = await CrearTecnicoAsync() });

        Assert.That(sobreCerrada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(sobreCerrada), Is.EqualTo("ORDEN_CERRADA"));
        Assert.That(sobreEliminada.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(tecnicoEnEliminada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(tecnicoEnEliminada), Is.EqualTo("ORDEN_NO_MODIFICABLE"));
    }

    [Test]
    public async Task AsignarUnTecnicoNoCambiaElEstadoYQuedaEnElHistorialDeTecnicos()
    {
        var orden = await CrearOrdenAsync();
        var id = orden.GetProperty("id").GetInt64();
        var tecnicoId = await CrearTecnicoAsync();

        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/tecnico", new { tecnicoId });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualizada = await respuesta.LeerJsonAsync();
        Assert.That(actualizada.GetProperty("estado").GetString(), Is.EqualTo("pendiente"));
        Assert.That(actualizada.GetProperty("tecnicoId").GetInt64(), Is.EqualTo(tecnicoId));
        Assert.That(actualizada.GetProperty("historialEstados").GetArrayLength(), Is.EqualTo(1));
        var tecnicos = actualizada.GetProperty("historialTecnicos").EnumerateArray().ToList();
        Assert.That(tecnicos, Has.Count.EqualTo(1));
        Assert.That(tecnicos[0].GetProperty("tecnicoId").GetInt64(), Is.EqualTo(tecnicoId));
        Assert.That(tecnicos[0].GetProperty("tecnicoAnteriorId").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task EnElAltaConTecnicoQuedaLaPrimeraAsignacionEnElHistorialDeTecnicos()
    {
        var tecnicoId = await CrearTecnicoAsync();

        var orden = await CrearOrdenAsync(tecnicoId);

        var tecnicos = orden.GetProperty("historialTecnicos").EnumerateArray().ToList();
        Assert.That(tecnicos.Select(t => t.GetProperty("tecnicoId").GetInt64()), Is.EqualTo(new[] { tecnicoId }));
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

    [TestCase("pendiente")]
    [TestCase("en_progreso")]
    public async Task RepetirElTecnicoActualResponde409SinCambiarLaOrdenNiElHistorial(string estado)
    {
        var tecnicoId = await CrearTecnicoAsync();
        var inicial = await CrearOrdenAsync(tecnicoId);
        var id = inicial.GetProperty("id").GetInt64();
        if (estado == "en_progreso") (await CambiarEstadoAsync(id, estado)).EnsureSuccessStatusCode();
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
        foreach (var tecnicoId in new[] { primero, segundo, primero })
        {
            var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/tecnico", new { tecnicoId });
            respuesta.EnsureSuccessStatusCode();
        }

        var final = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(final.GetProperty("tecnicoId").GetInt64(), Is.EqualTo(primero));
        Assert.That(final.GetProperty("historialEstados").GetArrayLength(), Is.EqualTo(1), "asignar un técnico no es un cambio de estado");
        var tecnicos = final.GetProperty("historialTecnicos").EnumerateArray().ToList();
        Assert.That(tecnicos.Select(t => t.GetProperty("tecnicoId").GetInt64()), Is.EqualTo(new[] { primero, segundo, primero }));
        Assert.That(tecnicos.Select(t => t.GetProperty("tecnicoAnteriorId").ValueKind == JsonValueKind.Null ? (long?)null : t.GetProperty("tecnicoAnteriorId").GetInt64()),
            Is.EqualTo(new long?[] { null, primero, segundo }));
    }

    [Test]
    public async Task AsignacionesSimultaneasDelMismoTecnicoSoloRegistranUnaAsignacion()
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
        Assert.That(final.GetProperty("historialTecnicos").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task UnaOrdenInexistenteResponde404()
    {
        Assert.That((await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{long.MaxValue}")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await CambiarEstadoAsync(long.MaxValue, "en_progreso")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
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
        (await CambiarEstadoAsync(otraOrden.GetProperty("id").GetInt64(), "eliminado", "Prueba de conteo histórico"))
            .EnsureSuccessStatusCode();

        async Task<int> Cantidad(string modulo, long recursoId, string campo)
            => (await (await Cliente.GetAsync($"/api/v1/{modulo}/{recursoId}")).LeerJsonAsync()).GetProperty(campo).GetInt32();

        // La orden eliminada no cuenta: solo las activas (no cerradas ni eliminadas).
        Assert.That(await Cantidad("usuarios", primero, "cantidadOrdenesAsignadas"), Is.EqualTo(1));
        Assert.That(await Cantidad("usuarios", segundo, "cantidadOrdenesAsignadas"), Is.Zero);
        Assert.That(await Cantidad("clientes", clienteId, "cantidadOrdenesAsociadas"), Is.EqualTo(1));
        Assert.That(await Cantidad("clientes", sinOrdenes.GetProperty("id").GetInt64(), "cantidadOrdenesAsociadas"), Is.Zero);
        async Task<int> TotalListado(string consulta)
            => (await (await Cliente.GetAsync("/api/v1/ordenes-trabajo?" + consulta)).LeerJsonAsync()).GetProperty("totalElementos").GetInt32();

        // El enlace del conteo abre el listado con el mismo filtro: las cifras deben coincidir y sin soloActivas aparece también la eliminada.
        Assert.That(await TotalListado($"clienteId={clienteId}&soloActivas=true"), Is.EqualTo(1));
        Assert.That(await TotalListado($"tecnicoId={primero}&soloActivas=true"), Is.EqualTo(1));
        Assert.That(await TotalListado($"clienteId={clienteId}"), Is.EqualTo(2));
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
        var id = await CrearOrdenConTecnicoAsync();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => CambiarEstadoAsync(id, "en_progreso")));

        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.BadRequest), Is.EqualTo(4));
        var orden = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        var enProgreso = orden.GetProperty("historialEstados").EnumerateArray()
            .Count(h => h.GetProperty("estado").GetString() == "en_progreso");
        Assert.That(enProgreso, Is.EqualTo(1));
    }

    [Test]
    public async Task ElListadoFiltradoPorFaseDevuelveLasOrdenesDeTodosLosEstadosDeLaFase()
    {
        var id = await CrearOrdenConTecnicoAsync();

        var pendientes = await (await Cliente.GetAsync("/api/v1/ordenes-trabajo?fase=pendiente&tamanoPagina=100")).LeerJsonAsync();
        var enCurso = await (await Cliente.GetAsync("/api/v1/ordenes-trabajo?fase=en_curso&tamanoPagina=100")).LeerJsonAsync();

        var estadosPendientes = pendientes.GetProperty("elementos").EnumerateArray().Select(o => o.GetProperty("estado").GetString()).Distinct();
        Assert.That(estadosPendientes, Is.EqualTo(new[] { "pendiente" }));
        Assert.That(pendientes.GetProperty("elementos").EnumerateArray().Select(o => o.GetProperty("id").GetInt64()), Does.Contain(id));
        Assert.That(enCurso.GetProperty("elementos").EnumerateArray().Select(o => o.GetProperty("id").GetInt64()), Does.Not.Contain(id));
    }

    [Test]
    public async Task ElListadoPaginadoYFiltradoPorEstadoDevuelveSoloEsasOrdenes()
    {
        var id = await CrearOrdenConTecnicoAsync();

        var pagina = await (await Cliente.GetAsync("/api/v1/ordenes-trabajo?estado=pendiente&tamanoPagina=100")).LeerJsonAsync();

        var estados = pagina.GetProperty("elementos").EnumerateArray().Select(o => o.GetProperty("estado").GetString()).Distinct();
        Assert.That(estados, Is.EqualTo(new[] { "pendiente" }));
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
            repuestos = Array.Empty<object>(),
            estado,
            motivoEstado,
        });
        return await Cliente.SendAsync(solicitud);
    }

    private async Task<(long Id, JsonElement Orden)> CrearOrdenConTecnicoCompletaAsync()
    {
        var id = await CrearOrdenConTecnicoAsync();
        var orden = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        return (id, orden);
    }

    [Test]
    public async Task LaLecturaYLaModificacionDevuelvenLaVersionComoETag()
    {
        var (id, orden) = await CrearOrdenConTecnicoCompletaAsync();

        var lectura = await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}");
        var version = (await lectura.LeerJsonAsync()).GetProperty("version").GetString();

        Assert.That(version, Is.Not.Empty);
        Assert.That(lectura.Headers.ETag?.Tag, Is.EqualTo($"\"{version}\""));

        var modificada = await ModificarAsync(id, orden, "pendiente", "Con versión nueva", ifMatch: $"\"{version}\"");
        Assert.That(modificada.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var versionNueva = (await modificada.LeerJsonAsync()).GetProperty("version").GetString();
        Assert.That(versionNueva, Is.Not.EqualTo(version));
        Assert.That(modificada.Headers.ETag?.Tag, Is.EqualTo($"\"{versionNueva}\""));
    }

    [Test]
    public async Task ModificarConUnaVersionVencidaResponde412YNoCambiaNada()
    {
        var (id, orden) = await CrearOrdenConTecnicoCompletaAsync();
        var version = orden.GetProperty("version").GetString();
        (await ModificarAsync(id, orden, "pendiente", "Primera edición", ifMatch: $"\"{version}\"")).EnsureSuccessStatusCode();

        var tardia = await ModificarAsync(id, orden, "pendiente", "Segunda edición con datos viejos", ifMatch: $"\"{version}\"");

        Assert.That(tardia.StatusCode, Is.EqualTo(HttpStatusCode.PreconditionFailed));
        Assert.That(await LeerCodigoAsync(tardia), Is.EqualTo("ORDEN_MODIFICADA"));
        var actual = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(actual.GetProperty("descripcion").GetString(), Is.EqualTo("Primera edición"));
    }

    [Test]
    public async Task ModificarSinIfMatchOConAsteriscoMantieneElComportamientoAnterior()
    {
        var (id, orden) = await CrearOrdenConTecnicoCompletaAsync();

        var sinEncabezado = await ModificarAsync(id, orden, "pendiente", "Sin encabezado");
        var conAsterisco = await ModificarAsync(id, orden, "pendiente", "Con asterisco", ifMatch: "*");

        Assert.That(sinEncabezado.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(conAsterisco.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task DosEdicionesSimultaneasConLaMismaVersionSoloAplicanUna()
    {
        var (id, orden) = await CrearOrdenConTecnicoCompletaAsync();
        var version = orden.GetProperty("version").GetString();

        var respuestas = await Task.WhenAll(Enumerable.Range(1, 5)
            .Select(n => ModificarAsync(id, orden, "pendiente", $"Edición {n}", ifMatch: $"\"{version}\"")));

        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.PreconditionFailed), Is.EqualTo(4));
    }

    [Test]
    public async Task ModificarUnaOrdenCambiaSusDatosYPuedeAvanzarElEstado()
    {
        var (id, orden) = await CrearOrdenConTecnicoCompletaAsync();

        var respuesta = await ModificarAsync(id, orden, "en_progreso", "Descripción corregida");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualizada = await respuesta.LeerJsonAsync();
        Assert.That(actualizada.GetProperty("descripcion").GetString(), Is.EqualTo("Descripción corregida"));
        Assert.That(actualizada.GetProperty("prioridad").GetString(), Is.EqualTo("alta"));
        Assert.That(actualizada.GetProperty("estado").GetString(), Is.EqualTo("en_progreso"));
    }

    [Test]
    public async Task ModificarUnaOrdenInexistenteResponde404UnaCerradaResponde409YUnaEliminadaTambien()
    {
        var (id, orden) = await CrearOrdenConTecnicoCompletaAsync();
        foreach (var estado in new[] { "en_progreso", "en_revision" })
        {
            (await CambiarEstadoAsync(id, estado)).EnsureSuccessStatusCode();
        }

        (await CambiarEstadoAsync(id, "cerrado")).EnsureSuccessStatusCode();
        var (idEliminada, ordenEliminada) = await CrearOrdenConTecnicoCompletaAsync();
        (await CambiarEstadoAsync(idEliminada, "eliminado", "Duplicada")).EnsureSuccessStatusCode();

        var inexistente = await ModificarAsync(long.MaxValue, orden, "pendiente", "x");
        var cerrada = await ModificarAsync(id, orden, "cerrado", "Cambio tardío");
        var eliminada = await ModificarAsync(idEliminada, ordenEliminada, "eliminado", "Cambio tardío");

        Assert.That(inexistente.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(cerrada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(cerrada), Is.EqualTo("ORDEN_CERRADA"));
        Assert.That(eliminada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(eliminada), Is.EqualTo("ORDEN_NO_MODIFICABLE"));
    }

    [Test]
    public async Task UnaTransicionInvalidaPorPutResponde400YNoCambiaNada()
    {
        var (id, orden) = await CrearOrdenConTecnicoCompletaAsync();

        var respuesta = await ModificarAsync(id, orden, "en_revision", "Salto inválido", "Motivo");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("TRANSICION_INVALIDA"));
        var sinCambios = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(sinCambios.GetProperty("estado").GetString(), Is.EqualTo("pendiente"));
        Assert.That(sinCambios.GetProperty("descripcion").GetString(), Is.Not.EqualTo("Salto inválido"));
    }

    [Test]
    public async Task CincoModificacionesSimultaneasAlMismoEstadoSoloRegistranUnaVezElCambio()
    {
        var (id, orden) = await CrearOrdenConTecnicoCompletaAsync();

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(i => ModificarAsync(id, orden, "en_progreso", "Edición " + i)));

        Assert.That(respuestas.Select(r => r.StatusCode), Has.All.EqualTo(HttpStatusCode.OK));
        var final = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        var enProgreso = final.GetProperty("historialEstados").EnumerateArray()
            .Count(h => h.GetProperty("estado").GetString() == "en_progreso");
        Assert.That(enProgreso, Is.EqualTo(1));
    }

    [Test]
    public async Task ListarEstadosDevuelveElCatalogoDeLaBaseEnElOrdenDelCiclo()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/ordenes-trabajo/estados");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var estados = await respuesta.LeerJsonAsync();
        var valores = estados.EnumerateArray().Select(estado => estado.GetProperty("valor").GetString()).ToList();
        Assert.That(valores, Is.EqualTo(new[] { "pendiente", "en_progreso", "en_revision", "cerrado", "pausado", "eliminado" }));
        var etiquetas = estados.EnumerateArray().Select(estado => estado.GetProperty("etiqueta").GetString()).ToList();
        Assert.That(etiquetas, Is.EqualTo(new[] { "Pendiente", "En progreso", "En revisión", "Cerrado", "Pausado", "Eliminado" }));
        Assert.That(estados.EnumerateArray().Select(estado => estado.GetProperty("orden").GetInt32()), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }));
        Assert.That(estados.EnumerateArray().Select(estado => estado.GetProperty("fase").GetString()),
            Is.EqualTo(new[] { "pendiente", "en_curso", "por_validar", "cerrada", "en_curso", "eliminada" }));
        Assert.That(estados.EnumerateArray().Select(estado => estado.GetProperty("tono").GetString()),
            Is.EqualTo(new[] { "neutro", "info", "destacado", "exito", "alerta", "neutro" }));
    }

    private static async Task<T> EnUnAlcanceAsync<T>(Func<SOTRA_BACKEND.Operaciones.OrdenTrabajoDAO, Task<T>> accion)
    {
        using var alcance = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.CreateScope(EntornoDePruebas.Fabrica.Services);
        return await accion(Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<SOTRA_BACKEND.Operaciones.OrdenTrabajoDAO>(alcance.ServiceProvider));
    }

    [Test]
    public async Task CadaCambioDeEstadoYDeTecnicoGuardaQuienLoHizoYElMotivo()
    {
        var actor = await CrearTecnicoAsync();
        var otroTecnico = await CrearTecnicoAsync();
        var id = (await CrearOrdenAsync(actor)).GetProperty("id").GetInt64();

        await EnUnAlcanceAsync(async dao => await dao.CambiarEstadoAsync((await dao.SeleccionarAsync(id, default))!, "en_progreso", null, false, actor, default));
        await EnUnAlcanceAsync(async dao => await dao.CambiarEstadoAsync((await dao.SeleccionarAsync(id, default))!, "pausado", "Falta de repuesto", false, actor, default));
        await EnUnAlcanceAsync(async dao => await dao.AsignarTecnicoAsync((await dao.SeleccionarAsync(id, default))!, otroTecnico, actor, default));

        var orden = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        var historial = orden.GetProperty("historialEstados").EnumerateArray().ToList();
        Assert.That(historial.Select(h => h.GetProperty("estado").GetString()), Is.EqualTo(new[] { "pendiente", "en_progreso", "pausado" }));
        Assert.That(historial.Select(h => h.GetProperty("estadoAnterior").GetString()), Is.EqualTo(new[] { null, "pendiente", "en_progreso" }));
        Assert.That(historial[2].GetProperty("observacion").GetString(), Is.EqualTo("Falta de repuesto"));
        Assert.That(historial[1].GetProperty("usuarioId").GetInt64(), Is.EqualTo(actor));
        Assert.That(historial[2].GetProperty("usuarioId").GetInt64(), Is.EqualTo(actor));
        Assert.That(historial[2].GetProperty("nombreUsuario").GetString(), Is.Not.Empty);
        var tecnicos = orden.GetProperty("historialTecnicos").EnumerateArray().ToList();
        Assert.That(tecnicos, Has.Count.EqualTo(2));
        Assert.That(tecnicos[1].GetProperty("tecnicoAnteriorId").GetInt64(), Is.EqualTo(actor));
        Assert.That(tecnicos[1].GetProperty("tecnicoId").GetInt64(), Is.EqualTo(otroTecnico));
        Assert.That(tecnicos[1].GetProperty("usuarioId").GetInt64(), Is.EqualTo(actor));
    }

    [Test]
    public async Task DevolverUnaOrdenEnRevisionAPendienteExigeMotivoYQuedaRegistrado()
    {
        var id = await CrearOrdenConTecnicoAsync();
        foreach (var estado in new[] { "en_progreso", "en_revision" })
        {
            (await CambiarEstadoAsync(id, estado)).EnsureSuccessStatusCode();
        }

        var sinMotivo = await CambiarEstadoAsync(id, "pendiente", " ");
        var conMotivo = await CambiarEstadoAsync(id, "pendiente", "Falta revisar el sello");

        Assert.That(sinMotivo.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(sinMotivo), Is.EqualTo("MOTIVO_OBLIGATORIO"));
        Assert.That(conMotivo.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var ultimo = (await conMotivo.LeerJsonAsync()).GetProperty("historialEstados").EnumerateArray().Last();
        Assert.That(ultimo.GetProperty("estadoAnterior").GetString(), Is.EqualTo("en_revision"));
        Assert.That(ultimo.GetProperty("estado").GetString(), Is.EqualTo("pendiente"));
        Assert.That(ultimo.GetProperty("observacion").GetString(), Is.EqualTo("Falta revisar el sello"));
    }

    [Test]
    public async Task UnaOrdenEliminadaSigueEnLaBaseConSuHistorialYSeFiltraPorEstado()
    {
        var id = (await CrearOrdenAsync()).GetProperty("id").GetInt64();

        (await CambiarEstadoAsync(id, "eliminado", "Cargada por error")).EnsureSuccessStatusCode();

        var detalle = await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();
        Assert.That(detalle.GetProperty("estado").GetString(), Is.EqualTo("eliminado"));
        Assert.That(detalle.GetProperty("transiciones").GetArrayLength(), Is.Zero);
        var pagina = await (await Cliente.GetAsync("/api/v1/ordenes-trabajo?estado=eliminado&tamanoPagina=100")).LeerJsonAsync();
        Assert.That(pagina.GetProperty("elementos").EnumerateArray().Select(o => o.GetProperty("id").GetInt64()), Does.Contain(id));
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
