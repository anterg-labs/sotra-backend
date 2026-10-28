using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

/// <summary>Comparativa, envejecimiento, pausas por motivo, avisos, análisis de desgaste y calendario de órdenes.</summary>
public partial class AnaliticaAvanzadaPruebas : PruebaBase
{
    private const long RolTecnico = 3;

    private static string Hoy => DateTime.UtcNow.ToString("yyyy-MM-dd");

    private async Task<long> CrearOrdenAsync(JsonElement activo, string tipo, string prioridad = "media", DateOnly? fechaPrevista = null)
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo,
            prioridad,
            descripcion = "Orden para analítica avanzada",
            fechaPrevistaEjecucion = fechaPrevista ?? DateOnly.FromDateTime(DateTime.UtcNow),
            repuestos = Array.Empty<object>(),
        });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created), await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private async Task AsignarTecnicoAsync(long ordenId)
    {
        var tecnico = await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [RolTecnico]));
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{ordenId}/tecnico",
            new { tecnicoId = tecnico.GetProperty("id").GetInt64() })).EnsureSuccessStatusCode();
    }

    private async Task CerrarAsync(long ordenId)
    {
        await AsignarTecnicoAsync(ordenId);
        foreach (var estado in new[] { "en_progreso", "en_revision", "cerrado" })
        {
            (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{ordenId}/estado", new { estado })).EnsureSuccessStatusCode();
        }
    }

    private async Task<JsonElement> LeerAsync(string ruta)
    {
        var respuesta = await Cliente.GetAsync(ruta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), ruta + " " + await respuesta.Content.ReadAsStringAsync());
        return await respuesta.LeerJsonAsync();
    }

    private static JsonElement Metrica(JsonElement comparativa, string codigo) =>
        comparativa.GetProperty("metricas").EnumerateArray().Single(metrica => metrica.GetProperty("codigo").GetString() == codigo);

    [Test]
    public async Task LaComparativaCalculaPreventivasCumplimientoYTiempoDeReparacion()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var correctiva = await CrearOrdenAsync(activo, "correctivo");
        await CrearOrdenAsync(activo, "correctivo");
        var preventiva = await CrearOrdenAsync(activo, "preventivo");
        await CerrarAsync(correctiva);
        await CerrarAsync(preventiva);

        var comparativa = await LeerAsync($"/api/v1/reportes/analisis/comparativa?clienteId={clienteId}&fechaDesde={Hoy}&fechaHasta={Hoy}");

        Assert.That(comparativa.GetProperty("periodo").GetProperty("desde").GetString(), Is.EqualTo(Hoy));
        Assert.That(Metrica(comparativa, "ordenes_creadas").GetProperty("actual").GetDouble(), Is.EqualTo(3));
        Assert.That(Metrica(comparativa, "preventivas_porcentaje").GetProperty("actual").GetDouble(), Is.EqualTo(33.33).Within(0.01));
        Assert.That(Metrica(comparativa, "cumplimiento_preventivo").GetProperty("actual").GetDouble(), Is.EqualTo(100));
        var reparacion = Metrica(comparativa, "tiempo_reparacion_horas");
        Assert.That(reparacion.GetProperty("actual").ValueKind, Is.EqualTo(JsonValueKind.Number));
        Assert.That(reparacion.GetProperty("mejorSiSube").GetBoolean(), Is.False);
        Assert.That(reparacion.GetProperty("serie").GetArrayLength(), Is.EqualTo(6));
        Assert.That(reparacion.GetProperty("anterior").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task LaComparativaRechazaUnRangoInvertido()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/reportes/analisis/comparativa?fechaDesde=2026-10-10&fechaHasta=2026-10-01");
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo("RANGO_FECHAS_INVALIDO"));
    }

    [Test]
    public async Task LasTendenciasInformanTambienLasOrdenesCerradasDelPeriodo()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        await CrearOrdenAsync(activo, "correctivo");
        var cerrada = await CrearOrdenAsync(activo, "correctivo");
        await CerrarAsync(cerrada);

        var tendencias = await LeerAsync($"/api/v1/reportes/tendencias?clienteId={clienteId}&agrupacion=mes");

        var punto = tendencias.GetProperty("puntos")[0];
        Assert.That(punto.GetProperty("total").GetInt32(), Is.EqualTo(2));
        Assert.That(punto.GetProperty("cerradas").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task ElEnvejecimientoAgrupaLasAbiertasPorAntiguedadYCalculaElBacklog()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        await CrearOrdenAsync(activo, "correctivo");
        await CrearOrdenAsync(activo, "preventivo");
        var cerrada = await CrearOrdenAsync(activo, "correctivo");
        await CerrarAsync(cerrada);

        var envejecimiento = await LeerAsync($"/api/v1/reportes/envejecimiento?clienteId={clienteId}");

        Assert.That(envejecimiento.GetProperty("abiertas").GetInt32(), Is.EqualTo(2));
        var rangos = envejecimiento.GetProperty("rangos");
        Assert.That(rangos.GetArrayLength(), Is.EqualTo(5));
        Assert.That(rangos[0].GetProperty("rango").GetString(), Is.EqualTo("hasta_3"));
        Assert.That(rangos[0].GetProperty("cantidad").GetInt32(), Is.EqualTo(2));
        Assert.That(rangos.EnumerateArray().Sum(rango => rango.GetProperty("cantidad").GetInt32()), Is.EqualTo(2));
        Assert.That(envejecimiento.GetProperty("cierresPorSemana").GetDouble(), Is.GreaterThan(0));
        Assert.That(envejecimiento.GetProperty("backlogEnSemanas").GetDouble(), Is.GreaterThan(0));
        Assert.That(envejecimiento.GetProperty("masAntiguas").GetArrayLength(), Is.EqualTo(2));
    }

    [Test]
    public async Task ElCumplimientoSeSeparaPorTipoDeOrdenYPorTipoDeMaquina()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var preventiva = await CrearOrdenAsync(activo, "preventivo");
        var atrasada = await CrearOrdenAsync(activo, "preventivo", fechaPrevista: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-5)));
        await CerrarAsync(preventiva);
        await CerrarAsync(atrasada);

        var tiempos = await LeerAsync($"/api/v1/reportes/tiempos?clienteId={clienteId}");

        var porTipo = tiempos.GetProperty("cumplimientoPorTipoDeOrden").EnumerateArray().Single();
        Assert.That(porTipo.GetProperty("nombre").GetString(), Is.EqualTo("preventivo"));
        Assert.That(porTipo.GetProperty("evaluadas").GetInt32(), Is.EqualTo(2));
        Assert.That(porTipo.GetProperty("enPlazo").GetInt32(), Is.EqualTo(1));
        Assert.That(porTipo.GetProperty("porcentajeEnPlazo").GetDouble(), Is.EqualTo(50));
        var porMaquina = tiempos.GetProperty("cumplimientoPreventivoPorTipoDeMaquina").EnumerateArray().Single();
        Assert.That(porMaquina.GetProperty("evaluadas").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task ElDesempenoDaLasPausasPorMotivoElParetoYElDetallePorModelo()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var orden = await CrearOrdenAsync(activo, "correctivo");
        await AsignarTecnicoAsync(orden);
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{orden}/estado", new { estado = "en_progreso" })).EnsureSuccessStatusCode();
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{orden}/estado",
            new { estado = "pausado", motivo = "No hay filtro", motivoCodigo = "falta_repuesto" })).EnsureSuccessStatusCode();
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{orden}/estado", new { estado = "en_progreso" })).EnsureSuccessStatusCode();
        foreach (var estado in new[] { "en_revision", "cerrado" })
        {
            (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{orden}/estado", new { estado })).EnsureSuccessStatusCode();
        }

        var desempeno = await LeerAsync($"/api/v1/reportes/desempeno?clienteId={clienteId}");

        var motivo = desempeno.GetProperty("flujo").GetProperty("pausas").GetProperty("porMotivo").EnumerateArray().Single();
        Assert.That(motivo.GetProperty("codigo").GetString(), Is.EqualTo("falta_repuesto"));
        Assert.That(motivo.GetProperty("cantidad").GetInt32(), Is.EqualTo(1));
        var maquinaria = desempeno.GetProperty("maquinaria");
        var activoItem = maquinaria.GetProperty("activos").EnumerateArray().Single();
        Assert.That(activoItem.GetProperty("correctivasCerradas").GetInt32(), Is.EqualTo(1));
        Assert.That(activoItem.GetProperty("tiempoMedioReparacionHoras").ValueKind, Is.EqualTo(JsonValueKind.Number));
        Assert.That(activoItem.GetProperty("porcentajeAcumulado").GetDouble(), Is.EqualTo(100));
        var modelo = maquinaria.GetProperty("porModelo").EnumerateArray().Single();
        Assert.That(modelo.GetProperty("activosEnServicio").GetInt32(), Is.GreaterThanOrEqualTo(1));
        Assert.That(maquinaria.GetProperty("porTipo").EnumerateArray().Single().GetProperty("costoParada").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(desempeno.GetProperty("estimacion").GetProperty("ordenes").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task UnActivoQueReincideGeneraUnAvisoCriticoConSuEvidencia()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        for (var i = 0; i < 6; i++)
        {
            await CrearOrdenAsync(activo, "correctivo");
        }

        var avisos = await LeerAsync("/api/v1/reportes/avisos");

        var aviso = avisos.GetProperty("avisos").EnumerateArray()
            .FirstOrDefault(item => item.GetProperty("regla").GetString() == "R1"
                && item.GetProperty("activoId").ValueKind == JsonValueKind.Number
                && item.GetProperty("activoId").GetInt64() == activo.GetProperty("id").GetInt64());
        Assert.That(aviso.ValueKind, Is.EqualTo(JsonValueKind.Object), "No se generó el aviso de reincidencia");
        Assert.That(aviso.GetProperty("severidad").GetString(), Is.EqualTo("critico"));
        Assert.That(aviso.GetProperty("evidencia").GetProperty("valor").GetDouble(), Is.EqualTo(6));
        Assert.That(aviso.GetProperty("evidencia").GetProperty("umbral").GetDouble(), Is.EqualTo(3));
        Assert.That(aviso.GetProperty("evidencia").GetProperty("ventanaDias").GetInt32(), Is.EqualTo(730));
        Assert.That(avisos.GetProperty("total").GetInt32(), Is.EqualTo(avisos.GetProperty("avisos").GetArrayLength()));
    }

    [Test]
    public async Task LosAvisosNuncaSeRefierenAPersonas()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenAsync(activo, "correctivo", prioridad: "alta");
        await CrearOrdenAsync(activo, "correctivo");

        var texto = (await Cliente.GetAsync("/api/v1/reportes/avisos")).Content.ReadAsStringAsync().Result.ToLowerInvariant();

        Assert.That(texto, Does.Not.Contain("tecnicoid"));
        Assert.That(texto, Does.Not.Contain("nombretecnico"));
        Assert.That(texto, Does.Not.Contain("usuarioid"));
    }

    [Test]
    public async Task LasReglasDeAvisoSeConfiguranSinMinimosFijosYUnaReglaApagadaNoAvisa()
    {
        var reglas = await LeerAsync("/api/v1/reportes/avisos/reglas");
        Assert.That(reglas.GetArrayLength(), Is.GreaterThanOrEqualTo(9));
        var r1 = reglas.EnumerateArray().Single(regla => regla.GetProperty("codigo").GetString() == "R1");
        var activa = r1.GetProperty("activa").GetBoolean();
        var umbral = r1.GetProperty("umbral").GetDecimal();
        var critico = r1.GetProperty("umbralCritico").GetDecimal();

        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenAsync(activo, "correctivo");
        await CrearOrdenAsync(activo, "correctivo");

        try
        {
            var apagada = await Cliente.PutAsJsonAsync("/api/v1/reportes/avisos/reglas/R1", new
            {
                activa = false,
                umbral,
                umbralCritico = critico,
                ventanaDias = 180,
                ventanaCriticaDias = 730,
                minimoDatos = 1,
            });
            Assert.That(apagada.StatusCode, Is.EqualTo(HttpStatusCode.OK), await apagada.Content.ReadAsStringAsync());
            var avisos = await LeerAsync("/api/v1/reportes/avisos");
            Assert.That(avisos.GetProperty("avisos").EnumerateArray().Any(aviso => aviso.GetProperty("regla").GetString() == "R1"), Is.False);

            var sinTope = await Cliente.PutAsJsonAsync("/api/v1/reportes/avisos/reglas/R1", new
            {
                activa = true,
                umbral = 100000,
                umbralCritico = 200000,
                ventanaDias = 5000,
                ventanaCriticaDias = 9000,
                minimoDatos = 0,
            });
            Assert.That(sinTope.StatusCode, Is.EqualTo(HttpStatusCode.OK), "Los umbrales no tienen límites fijos");
        }
        finally
        {
            (await Cliente.PutAsJsonAsync("/api/v1/reportes/avisos/reglas/R1", new
            {
                activa,
                umbral,
                umbralCritico = critico,
                ventanaDias = 180,
                ventanaCriticaDias = 730,
                minimoDatos = 1,
            })).EnsureSuccessStatusCode();
        }
    }

    [Test]
    public async Task UnaReglaInexistenteOConVentanaInvalidaSeRechaza()
    {
        var inexistente = await Cliente.PutAsJsonAsync("/api/v1/reportes/avisos/reglas/R99",
            new { activa = true, umbral = 1, minimoDatos = 1 });
        Assert.That(inexistente.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));

        var invalida = await Cliente.PutAsJsonAsync("/api/v1/reportes/avisos/reglas/R7",
            new { activa = true, umbral = 30, ventanaDias = 0, minimoDatos = 1 });
        Assert.That(invalida.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await invalida.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo("REGLA_INVALIDA"));
    }

    [Test]
    public async Task LaRutaDeReparacionesAtipicasRetiradaRespondeNoEncontrado()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/reportes/analisis/atipicas");
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ElDesgasteAnalizaLosIntervalosDeUnActivoConSuficientesCorrectivasYAvisaDatosInsuficientes()
    {
        var sinDatos = await DatosDePrueba.CrearActivoAsync(Cliente);
        var vacio = await LeerAsync($"/api/v1/reportes/analisis/desgaste?clienteId={sinDatos.GetProperty("clienteId").GetInt64()}");
        Assert.That(vacio.GetProperty("datosInsuficientes").GetBoolean(), Is.True);

        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        for (var i = 0; i < 4; i++)
        {
            await CrearOrdenAsync(activo, "correctivo");
        }

        var desgaste = await LeerAsync($"/api/v1/reportes/analisis/desgaste?clienteId={activo.GetProperty("clienteId").GetInt64()}");
        var elemento = desgaste.GetProperty("activos").EnumerateArray().Single();
        Assert.That(elemento.GetProperty("intervalosDias").GetArrayLength(), Is.EqualTo(3));
        Assert.That(elemento.GetProperty("unidadUso").GetString(), Is.EqualTo("horas"));
        Assert.That(desgaste.TryGetProperty("patrones", out _), Is.False);
        Assert.That(desgaste.GetProperty("datosInsuficientes").GetBoolean(), Is.False);
    }

    [Test]
    public async Task ElCalendarioUbicaLasOrdenesPorSuFechaPrevistaYOcultaLasEliminadas()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var dia = new DateOnly(2031, 5, 10);
        var visible = await CrearOrdenAsync(activo, "preventivo", fechaPrevista: dia);
        var eliminada = await CrearOrdenAsync(activo, "correctivo", fechaPrevista: dia);
        await CrearOrdenAsync(activo, "correctivo", fechaPrevista: new DateOnly(2031, 7, 1));
        var respuestaEliminar = await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{eliminada}/estado",
            new { estado = "eliminado", motivo = "Creada por error" });
        Assert.That(respuestaEliminar.StatusCode, Is.EqualTo(HttpStatusCode.OK), await respuestaEliminar.Content.ReadAsStringAsync());

        var calendario = await LeerAsync($"/api/v1/ordenes-trabajo/calendario?desde=2031-05-01&hasta=2031-05-31&clienteId={clienteId}");
        var elementos = calendario.GetProperty("elementos");
        Assert.That(elementos.GetArrayLength(), Is.EqualTo(1));
        Assert.That(elementos[0].GetProperty("id").GetInt64(), Is.EqualTo(visible));
        Assert.That(elementos[0].GetProperty("fecha").GetString(), Is.EqualTo("2031-05-10"));
        Assert.That(elementos[0].GetProperty("tipo").GetString(), Is.EqualTo("preventivo"));
        Assert.That(elementos[0].GetProperty("fase").GetString(), Is.EqualTo("pendiente"));
        Assert.That(elementos[0].GetProperty("vencida").GetBoolean(), Is.False);
        Assert.That(calendario.GetProperty("truncado").GetBoolean(), Is.False);

        var conEliminadas = await LeerAsync(
            $"/api/v1/ordenes-trabajo/calendario?desde=2031-05-01&hasta=2031-05-31&clienteId={clienteId}&incluirEliminadas=true");
        Assert.That(conEliminadas.GetProperty("elementos").GetArrayLength(), Is.EqualTo(2));

        var soloCorrectivas = await LeerAsync(
            $"/api/v1/ordenes-trabajo/calendario?desde=2031-05-01&hasta=2031-07-31&clienteId={clienteId}&tipo=correctivo");
        Assert.That(soloCorrectivas.GetProperty("elementos").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task ElCalendarioMarcaLasVencidasYRechazaRangosInvalidos()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        await CrearOrdenAsync(activo, "correctivo", fechaPrevista: new DateOnly(2020, 1, 15));

        var calendario = await LeerAsync($"/api/v1/ordenes-trabajo/calendario?desde=2020-01-01&hasta=2020-01-31&clienteId={clienteId}");
        Assert.That(calendario.GetProperty("elementos")[0].GetProperty("vencida").GetBoolean(), Is.True);

        var invertido = await Cliente.GetAsync("/api/v1/ordenes-trabajo/calendario?desde=2026-02-10&hasta=2026-02-01");
        Assert.That(invertido.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var muyAmplio = await Cliente.GetAsync("/api/v1/ordenes-trabajo/calendario?desde=2026-01-01&hasta=2026-12-31");
        Assert.That((await muyAmplio.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo("RANGO_FECHAS_INVALIDO"));
        var sinFechas = await Cliente.GetAsync("/api/v1/ordenes-trabajo/calendario");
        Assert.That(sinFechas.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
