using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class AnaliticaPruebas : PruebaBase
{
    private const long RolTecnico = 3;

    private static string Hoy => DateTime.UtcNow.ToString("yyyy-MM-dd");

    private async Task<long> CrearOrdenAsync(JsonElement activo, string tipo, long? tecnicoId = null, long[]? repuestosIds = null)
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tecnicoId,
            tipo,
            prioridad = "media",
            descripcion = "Orden para analítica",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow),
            repuestos = (repuestosIds ?? []).Select(id => new { repuestoId = id, cantidad = 1 }),
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private async Task CerrarAsync(long ordenId)
    {
        var tecnico = await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [RolTecnico]));
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{ordenId}/tecnico",
            new { tecnicoId = tecnico.GetProperty("id").GetInt64() })).EnsureSuccessStatusCode();
        foreach (var estado in new[] { "en_progreso", "en_revision", "cerrado" })
        {
            (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{ordenId}/estado", new { estado }))
                .EnsureSuccessStatusCode();
        }
    }

    private async Task<JsonElement> LeerAsync(string ruta)
    {
        var respuesta = await Cliente.GetAsync(ruta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), ruta);
        return await respuesta.LeerJsonAsync();
    }

    [Test]
    public async Task LasTendenciasContabilizanPreventivasYCorrectivasPorPeriodoDelCliente()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        await CrearOrdenAsync(activo, "correctivo");
        await CrearOrdenAsync(activo, "correctivo");
        await CrearOrdenAsync(activo, "preventivo");

        var tendencias = await LeerAsync($"/api/v1/reportes/tendencias?clienteId={clienteId}&agrupacion=mes");

        Assert.That(tendencias.GetProperty("agrupacion").GetString(), Is.EqualTo("mes"));
        Assert.That(tendencias.GetProperty("totalPeriodo").GetInt32(), Is.EqualTo(3));
        var punto = tendencias.GetProperty("puntos")[0];
        Assert.That(punto.GetProperty("correctivas").GetInt32(), Is.EqualTo(2));
        Assert.That(punto.GetProperty("preventivas").GetInt32(), Is.EqualTo(1));
        Assert.That(punto.GetProperty("periodo").GetString(), Does.EndWith("-01"));
        Assert.That(tendencias.GetProperty("totalPeriodoAnterior").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task ConDosFechasInformaElTotalDelPeriodoAnterior()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenAsync(activo, "correctivo");

        var tendencias = await LeerAsync(
            $"/api/v1/reportes/tendencias?clienteId={activo.GetProperty("clienteId").GetInt64()}&fechaDesde={Hoy}&fechaHasta={Hoy}");

        Assert.That(tendencias.GetProperty("agrupacion").GetString(), Is.EqualTo("semana"));
        Assert.That(tendencias.GetProperty("totalPeriodo").GetInt32(), Is.EqualTo(1));
        Assert.That(tendencias.GetProperty("totalPeriodoAnterior").GetInt32(), Is.EqualTo(0));
    }

    [TestCase("agrupacion=anio")]
    [TestCase("fechaDesde=2026-10-10&fechaHasta=2026-10-01")]
    public async Task UnosFiltrosInvalidosResponden400ConCodigoEstable(string consulta)
    {
        var respuesta = await Cliente.GetAsync("/api/v1/reportes/tendencias?" + consulta);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(),
            Is.AnyOf("AGRUPACION_INVALIDA", "RANGO_FECHAS_INVALIDO"));
    }

    [Test]
    public async Task LosTiemposMidenLaDuracionDeLasOrdenesCerradasYElTiempoPorEtapa()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var ordenId = await CrearOrdenAsync(activo, "correctivo");
        await CerrarAsync(ordenId);
        await CrearOrdenAsync(activo, "correctivo");

        var tiempos = await LeerAsync($"/api/v1/reportes/tiempos?clienteId={clienteId}");

        var duracion = tiempos.GetProperty("duracion");
        Assert.That(duracion.GetProperty("cantidad").GetInt32(), Is.EqualTo(1));
        Assert.That(duracion.GetProperty("promedioHoras").GetDouble(), Is.GreaterThanOrEqualTo(0));
        Assert.That(duracion.GetProperty("medianaHoras").GetDouble(), Is.GreaterThanOrEqualTo(0));
        var etapas = tiempos.GetProperty("etapas").EnumerateArray().Select(e => e.GetProperty("estado").GetString()).ToList();
        Assert.That(etapas, Is.SupersetOf(new[] { "pendiente", "en_progreso", "en_revision" }));
    }

    [Test]
    public async Task SinOrdenesCerradasLosTiemposVienenVaciosSinFallar()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);

        var tiempos = await LeerAsync($"/api/v1/reportes/tiempos?clienteId={activo.GetProperty("clienteId").GetInt64()}");

        Assert.That(tiempos.GetProperty("duracion").GetProperty("cantidad").GetInt32(), Is.EqualTo(0));
        Assert.That(tiempos.GetProperty("duracion").GetProperty("promedioHoras").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(tiempos.GetProperty("etapas").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task UnActivoConDosCorrectivasSeguidasEsReincidenteYUnoConUnaSolaNo()
    {
        var reincidente = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenAsync(reincidente, "correctivo");
        await CrearOrdenAsync(reincidente, "correctivo");
        var unico = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenAsync(unico, "correctivo");

        var resultado = await LeerAsync($"/api/v1/reportes/reincidencias?clienteId={reincidente.GetProperty("clienteId").GetInt64()}");
        var ninguno = await LeerAsync($"/api/v1/reportes/reincidencias?clienteId={unico.GetProperty("clienteId").GetInt64()}");
        var exigente = await LeerAsync($"/api/v1/reportes/reincidencias?clienteId={reincidente.GetProperty("clienteId").GetInt64()}&minimo=2");

        Assert.That(resultado.GetProperty("dias").GetInt32(), Is.EqualTo(90));
        var elementos = resultado.GetProperty("elementos");
        Assert.That(elementos.GetArrayLength(), Is.EqualTo(1));
        Assert.That(elementos[0].GetProperty("activoId").GetInt64(), Is.EqualTo(reincidente.GetProperty("id").GetInt64()));
        Assert.That(elementos[0].GetProperty("correctivas").GetInt32(), Is.EqualTo(2));
        Assert.That(elementos[0].GetProperty("reincidencias").GetInt32(), Is.EqualTo(1));
        Assert.That(ninguno.GetProperty("elementos").GetArrayLength(), Is.EqualTo(0));
        Assert.That(exigente.GetProperty("elementos").GetArrayLength(), Is.EqualTo(0));
    }

    [Test]
    public async Task LasPreventivasNoCuentanComoReincidencia()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenAsync(activo, "preventivo");
        await CrearOrdenAsync(activo, "preventivo");

        var resultado = await LeerAsync($"/api/v1/reportes/reincidencias?clienteId={activo.GetProperty("clienteId").GetInt64()}");

        Assert.That(resultado.GetProperty("elementos").GetArrayLength(), Is.EqualTo(0));
    }

    [TestCase("dias=0")]
    [TestCase("dias=5000")]
    [TestCase("minimo=0")]
    public async Task UnosParametrosDeReincidenciaFueraDeRangoResponden400(string consulta)
    {
        var respuesta = await Cliente.GetAsync("/api/v1/reportes/reincidencias?" + consulta);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ConLosUmbralesPorDefectoUnaOrdenRecienCreadaNoGeneraAlertas()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenAsync(activo, "correctivo");

        var alertas = await LeerAsync($"/api/v1/reportes/alertas-maquinaria?clienteId={activo.GetProperty("clienteId").GetInt64()}");

        Assert.That(alertas.GetProperty("total").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task ConUmbralesEnCeroLasOrdenesAbiertasGeneranAlertasCriticasOrdenadasYAjustadasPorConfiguracion()
    {
        var variables = new[]
        {
            "AlertasMaquinaria__SinAsignarHoras", "AlertasMaquinaria__AsignadaSinIniciarHoras",
            "AlertasMaquinaria__PausadaHoras", "AlertasMaquinaria__MaquinaDetenidaMediaHoras",
        };
        foreach (var variable in variables)
        {
            Environment.SetEnvironmentVariable(variable, "0");
        }

        try
        {
            await using var fabrica = new FabricaApi();
            using var cliente = fabrica.CreateClient();
            var activo = await DatosDePrueba.CrearActivoAsync(cliente);
            var respuestaOrden = await cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
            {
                clienteId = activo.GetProperty("clienteId").GetInt64(),
                activoId = activo.GetProperty("id").GetInt64(),
                tipo = "correctivo",
                prioridad = "media",
                descripcion = "Orden sin asignar",
                fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow),
            });
            respuestaOrden.EnsureSuccessStatusCode();
            var ordenId = (await respuestaOrden.LeerJsonAsync()).GetProperty("id").GetInt64();

            var alertas = await (await cliente.GetAsync(
                $"/api/v1/reportes/alertas-maquinaria?clienteId={activo.GetProperty("clienteId").GetInt64()}")).LeerJsonAsync();

            var tipos = alertas.GetProperty("alertas").EnumerateArray().Select(a => a.GetProperty("tipo").GetString()).ToList();
            Assert.That(tipos, Is.EquivalentTo(new[] { "sin_asignar", "maquina_detenida" }));
            Assert.That(alertas.GetProperty("criticas").GetInt32(), Is.EqualTo(2));
            var primera = alertas.GetProperty("alertas")[0];
            Assert.That(primera.GetProperty("ordenId").GetInt64(), Is.EqualTo(ordenId));
            Assert.That(primera.GetProperty("gravedad").GetString(), Is.EqualTo("critica"));
            Assert.That(primera.GetProperty("prioridad").GetString(), Is.EqualTo("media"));
            Assert.That(primera.GetProperty("umbralHoras").GetInt32(), Is.EqualTo(0));
        }
        finally
        {
            foreach (var variable in variables)
            {
                Environment.SetEnvironmentVariable(variable, null);
            }
        }
    }

    [Test]
    public async Task ElCumplimientoDeFechasPrevistasMideLasOrdenesCerradasEnPlazoYConAtraso()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var enPlazo = await CrearOrdenAsync(activo, "correctivo");
        var conAtraso = await CrearOrdenConFechaPrevistaAsync(activo, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-4)));
        await CerrarAsync(enPlazo);
        await CerrarAsync(conAtraso);

        var tiempos = await LeerAsync($"/api/v1/reportes/tiempos?clienteId={clienteId}");

        var cumplimiento = tiempos.GetProperty("cumplimiento");
        Assert.That(cumplimiento.GetProperty("evaluadas").GetInt32(), Is.EqualTo(2));
        Assert.That(cumplimiento.GetProperty("enPlazo").GetInt32(), Is.EqualTo(1));
        Assert.That(cumplimiento.GetProperty("porcentajeEnPlazo").GetDouble(), Is.EqualTo(50));
        Assert.That(cumplimiento.GetProperty("atrasoPromedioDias").GetDouble(), Is.EqualTo(4));
    }

    [Test]
    public async Task SinOrdenesCerradasElCumplimientoNoTienePorcentaje()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);

        var tiempos = await LeerAsync($"/api/v1/reportes/tiempos?clienteId={activo.GetProperty("clienteId").GetInt64()}");

        var cumplimiento = tiempos.GetProperty("cumplimiento");
        Assert.That(cumplimiento.GetProperty("evaluadas").GetInt32(), Is.EqualTo(0));
        Assert.That(cumplimiento.GetProperty("porcentajeEnPlazo").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    private async Task CambiarEstadoAsync(long ordenId, string estado, string? motivo = null) =>
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{ordenId}/estado", new { estado, motivo })).EnsureSuccessStatusCode();

    [Test]
    public async Task ElDesempenoMideOrdenesProcesosYMaquinariaSinEvaluarPersonas()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var primero = (await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [RolTecnico]))).GetProperty("id").GetInt64();
        var segundo = (await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [RolTecnico]))).GetProperty("id").GetInt64();
        var cerrada = await CrearOrdenAsync(activo, "correctivo", primero);
        await CambiarEstadoAsync(cerrada, "en_progreso");
        await CambiarEstadoAsync(cerrada, "pausado", "Falta material");
        await CambiarEstadoAsync(cerrada, "en_progreso");
        await CambiarEstadoAsync(cerrada, "en_revision");
        await CambiarEstadoAsync(cerrada, "pendiente", "Falta revisar el sello");
        await CambiarEstadoAsync(cerrada, "en_progreso");
        await CambiarEstadoAsync(cerrada, "en_revision");
        await CambiarEstadoAsync(cerrada, "cerrado");
        var abierta = await CrearOrdenAsync(activo, "preventivo", primero);
        (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{abierta}/tecnico", new { tecnicoId = segundo })).EnsureSuccessStatusCode();

        var desempeno = await LeerAsync($"/api/v1/reportes/desempeno?clienteId={clienteId}");

        var ordenes = desempeno.GetProperty("ordenes");
        Assert.That(ordenes.GetProperty("creadas").GetInt32(), Is.EqualTo(2));
        Assert.That(ordenes.GetProperty("cerradas").GetInt32(), Is.EqualTo(1));
        Assert.That(ordenes.GetProperty("abiertas").GetInt32(), Is.EqualTo(1));
        Assert.That(ordenes.GetProperty("eliminadas").GetInt32(), Is.Zero);
        Assert.That(ordenes.GetProperty("preventivas").GetInt32(), Is.EqualTo(1));
        Assert.That(ordenes.GetProperty("correctivas").GetInt32(), Is.EqualTo(1));
        Assert.That(ordenes.GetProperty("porcentajePreventivas").GetDouble(), Is.EqualTo(50));
        var tiempos = desempeno.GetProperty("tiempos");
        Assert.That(tiempos.GetProperty("respuesta").GetProperty("cantidad").GetInt32(), Is.EqualTo(1));
        Assert.That(tiempos.GetProperty("ejecucion").GetProperty("cantidad").GetInt32(), Is.EqualTo(1));
        Assert.That(tiempos.GetProperty("revision").GetProperty("cantidad").GetInt32(), Is.EqualTo(1));
        var flujo = desempeno.GetProperty("flujo");
        Assert.That(flujo.GetProperty("pausas").GetProperty("cantidad").GetInt32(), Is.EqualTo(1));
        Assert.That(flujo.GetProperty("pausas").GetProperty("ordenesConPausa").GetInt32(), Is.EqualTo(1));
        Assert.That(flujo.GetProperty("pausas").GetProperty("motivos")[0].GetProperty("texto").GetString(), Is.EqualTo("Falta material"));
        Assert.That(flujo.GetProperty("devoluciones").GetProperty("cantidad").GetInt32(), Is.EqualTo(1));
        Assert.That(flujo.GetProperty("devoluciones").GetProperty("porcentajeDeOrdenes").GetDouble(), Is.EqualTo(50));
        Assert.That(flujo.GetProperty("devoluciones").GetProperty("motivos")[0].GetProperty("texto").GetString(), Is.EqualTo("Falta revisar el sello"));
        Assert.That(flujo.GetProperty("reasignaciones").GetProperty("cantidad").GetInt32(), Is.EqualTo(1));
        var maquinaria = desempeno.GetProperty("maquinaria");
        Assert.That(maquinaria.GetProperty("porTipo").GetArrayLength(), Is.EqualTo(1));
        Assert.That(maquinaria.GetProperty("porTipo")[0].GetProperty("ordenes").GetInt32(), Is.EqualTo(2));
        var activoItem = maquinaria.GetProperty("activos")[0];
        Assert.That(activoItem.GetProperty("id").GetInt64(), Is.EqualTo(activo.GetProperty("id").GetInt64()));
        Assert.That(activoItem.GetProperty("correctivas").GetInt32(), Is.EqualTo(1));
        Assert.That(activoItem.GetProperty("horasParada").GetDouble(), Is.GreaterThanOrEqualTo(0));
        Assert.That(desempeno.GetRawText(), Does.Not.Contain("tecnico").And.Not.Contain("Tecnico"), "el reporte no desglosa por persona");
    }

    [Test]
    public async Task ElDesempenoSinOrdenesVieneEnCeroYSinPorcentajes()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);

        var desempeno = await LeerAsync($"/api/v1/reportes/desempeno?clienteId={activo.GetProperty("clienteId").GetInt64()}");

        Assert.That(desempeno.GetProperty("ordenes").GetProperty("creadas").GetInt32(), Is.Zero);
        Assert.That(desempeno.GetProperty("ordenes").GetProperty("porcentajePreventivas").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(desempeno.GetProperty("tiempos").GetProperty("respuesta").GetProperty("promedioHoras").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(desempeno.GetProperty("flujo").GetProperty("pausas").GetProperty("motivos").GetArrayLength(), Is.Zero);
        Assert.That(desempeno.GetProperty("maquinaria").GetProperty("activos").GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task ElDesempenoRechazaUnRangoDeFechasInvalido()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/reportes/desempeno?fechaDesde=2026-10-10&fechaHasta=2026-10-01");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task LosRepuestosMasUsadosSeOrdenanPorCantidadDeOrdenes()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var frecuente = await CrearRepuestoAsync();
        var raro = await CrearRepuestoAsync();
        await CrearOrdenAsync(activo, "correctivo", repuestosIds: [frecuente, raro]);
        await CrearOrdenAsync(activo, "correctivo", repuestosIds: [frecuente]);

        var resultado = await LeerAsync($"/api/v1/reportes/repuestos-usados?clienteId={clienteId}");

        var elementos = resultado.GetProperty("elementos");
        Assert.That(elementos.GetArrayLength(), Is.EqualTo(2));
        Assert.That(elementos[0].GetProperty("repuestoId").GetInt64(), Is.EqualTo(frecuente));
        Assert.That(elementos[0].GetProperty("ordenes").GetInt32(), Is.EqualTo(2));
        Assert.That(elementos[1].GetProperty("ordenes").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task El_reporte_csv_se_descarga_con_bom_y_encabezados()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/reportes/ordenes-trabajo/csv");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(respuesta.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/csv"));
        Assert.That(respuesta.Content.Headers.ContentDisposition?.FileName, Is.EqualTo("reporte-ordenes-trabajo.csv"));
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.That(bytes.Take(3), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
        Assert.That(System.Text.Encoding.UTF8.GetString(bytes), Does.Contain("N°;Creada;Cliente;Activo;Técnico;Tipo;Prioridad;Estado"));
    }

    [Test]
    public async Task El_reporte_csv_valida_el_tipo_y_el_rango_de_fechas()
    {
        var tipoInvalido = await Cliente.GetAsync("/api/v1/reportes/inexistente/csv");
        Assert.That(tipoInvalido.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await tipoInvalido.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo("TIPO_REPORTE_INVALIDO"));

        var rangoInvertido = await Cliente.GetAsync("/api/v1/reportes/indicadores/csv?fechaDesde=2026-02-01&fechaHasta=2026-01-01");
        Assert.That(rangoInvertido.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await rangoInvertido.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo("RANGO_FECHAS_INVALIDO"));
    }

    [TestCase("=HYPERLINK(\"http://x\")", "\"'=HYPERLINK(\"\"http://x\"\")\"")]
    [TestCase("+1", "'+1")]
    [TestCase("-2", "'-2")]
    [TestCase("@SUM(A1)", "'@SUM(A1)")]
    [TestCase("a;b", "\"a;b\"")]
    [TestCase("con \"comillas\"", "\"con \"\"comillas\"\"\"")]
    [TestCase("normal", "normal")]
    public void El_csv_neutraliza_formulas_y_escapa_separadores(string entrada, string esperado)
    {
        Assert.That(Api.Servicios.GeneradorReportesCsv.Escapar(entrada), Is.EqualTo(esperado));
    }

    private async Task<long> CrearRepuestoAsync()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "AN-" + DatosDePrueba.Sufijo(),
            nombre = "Repuesto de analítica",
            cantidad = 5,
            cantidadMinima = 1,
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private async Task<long> CrearOrdenConFechaPrevistaAsync(JsonElement activo, DateOnly fechaPrevista)
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Orden con fecha vencida",
            fechaPrevistaEjecucion = fechaPrevista,
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }
}
