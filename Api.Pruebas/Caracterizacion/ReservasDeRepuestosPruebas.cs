using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

/// <summary>
/// Cantidad de repuestos en una orden: reservado al asignarlo, entregado cuando la orden empieza, descontado al cerrarla
/// y liberado si se quita o la orden se elimina. El stock físico solo baja al cerrar.
/// </summary>
public class ReservasDeRepuestosPruebas : PruebaBase
{
    private const long RolTecnico = 3;

    private async Task<long> CrearRepuestoAsync(int cantidad, int minimo = 1)
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "RES-" + DatosDePrueba.Sufijo(),
            nombre = "Repuesto de reservas",
            cantidad,
            cantidadMinima = minimo,
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private async Task<long> CrearTecnicoAsync()
    {
        var tecnico = await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [RolTecnico]));
        return tecnico.GetProperty("id").GetInt64();
    }

    private async Task<HttpResponseMessage> CrearOrdenAsync(long? tecnicoId, params (long RepuestoId, int Cantidad)[] repuestos)
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        return await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tecnicoId,
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Orden con repuestos",
            observaciones = "",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2)),
            sugerencias = Array.Empty<string>(),
            repuestos = repuestos.Select(r => new { repuestoId = r.RepuestoId, cantidad = r.Cantidad }),
        });
    }

    private async Task<long> CrearOrdenOkAsync(long? tecnicoId, params (long RepuestoId, int Cantidad)[] repuestos)
    {
        var respuesta = await CrearOrdenAsync(tecnicoId, repuestos);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created), await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private Task<HttpResponseMessage> PonerRepuestosAsync(long ordenId, params (long RepuestoId, int Cantidad)[] repuestos) =>
        Cliente.PutAsJsonAsync($"/api/v1/ordenes-trabajo/{ordenId}/repuestos", new
        {
            repuestos = repuestos.Select(r => new { repuestoId = r.RepuestoId, cantidad = r.Cantidad }),
        });

    private Task<HttpResponseMessage> CambiarEstadoAsync(long ordenId, string estado, string? motivo = null) =>
        Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{ordenId}/estado", new { estado, motivo });

    private async Task<JsonElement> LeerRepuestoAsync(long id) =>
        await (await Cliente.GetAsync($"/api/v1/repuestos/{id}")).LeerJsonAsync();

    private async Task<JsonElement> LeerOrdenAsync(long id) =>
        await (await Cliente.GetAsync($"/api/v1/ordenes-trabajo/{id}")).LeerJsonAsync();

    // El cuerpo se lee como texto para poder consultar código y mensaje de la misma respuesta.
    private static async Task<string?> CodigoAsync(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement.GetProperty("codigo").GetString();

    private static async Task<string> MensajeAsync(HttpResponseMessage respuesta) =>
        JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync()).RootElement.GetProperty("mensaje").GetString()!;

    private async Task VerificarCantidadesAsync(long repuestoId, int fisico, int reservada, int entregada, int disponible)
    {
        var repuesto = await LeerRepuestoAsync(repuestoId);
        Assert.Multiple(() =>
        {
            Assert.That(repuesto.GetProperty("cantidad").GetInt32(), Is.EqualTo(fisico), "físico");
            Assert.That(repuesto.GetProperty("cantidadReservada").GetInt32(), Is.EqualTo(reservada), "reservada");
            Assert.That(repuesto.GetProperty("cantidadEntregada").GetInt32(), Is.EqualTo(entregada), "entregada");
            Assert.That(repuesto.GetProperty("cantidadDisponible").GetInt32(), Is.EqualTo(disponible), "disponible");
        });
    }

    [Test]
    public async Task AsignarUnaCantidadLaReservaSinBajarElStockFisico()
    {
        var repuesto = await CrearRepuestoAsync(10);

        var orden = await LeerOrdenAsync(await CrearOrdenOkAsync(null, (repuesto, 4)));

        var linea = orden.GetProperty("repuestos")[0];
        Assert.That(linea.GetProperty("cantidad").GetInt32(), Is.EqualTo(4));
        Assert.That(linea.GetProperty("estado").GetString(), Is.EqualTo("reservado"));
        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 4, entregada: 0, disponible: 6);
    }

    [Test]
    public async Task LoReservadoPorUnaOrdenNoSePuedeAsignarAOtra()
    {
        var repuesto = await CrearRepuestoAsync(10);
        await CrearOrdenOkAsync(null, (repuesto, 8));

        var respuesta = await CrearOrdenAsync(null, (repuesto, 3));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodigoAsync(respuesta), Is.EqualTo("STOCK_RESERVADO"));
        Assert.That(await MensajeAsync(respuesta), Does.Contain("8 reservadas por otras órdenes").And.Contain("quedan 2").And.Contain("se necesitan 3"));
        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 8, entregada: 0, disponible: 2);
    }

    [Test]
    public async Task LoEntregadoAUnTecnicoTampocoSePuedeAsignarAOtraOrden()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var ordenA = await CrearOrdenOkAsync(await CrearTecnicoAsync(), (repuesto, 9));
        (await CambiarEstadoAsync(ordenA, "en_progreso")).EnsureSuccessStatusCode();

        var respuesta = await CrearOrdenAsync(null, (repuesto, 2));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodigoAsync(respuesta), Is.EqualTo("STOCK_ENTREGADO"));
        Assert.That(await MensajeAsync(respuesta), Does.Contain("9 entregadas a otras órdenes"));
    }

    [Test]
    public async Task SiNoHayStockElErrorLoDiceDirectamente()
    {
        var sinStock = await CrearRepuestoAsync(0);
        var poco = await CrearRepuestoAsync(3);

        var enCero = await CrearOrdenAsync(null, (sinStock, 1));
        var deMas = await CrearOrdenAsync(null, (poco, 5));

        Assert.That(enCero.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodigoAsync(enCero), Is.EqualTo("STOCK_INSUFICIENTE"));
        Assert.That(await MensajeAsync(enCero), Does.StartWith("No hay stock de "));
        Assert.That(await CodigoAsync(deMas), Is.EqualTo("STOCK_INSUFICIENTE"));
        Assert.That(await MensajeAsync(deMas), Does.Contain("hay 3 y se necesitan 5"));
    }

    [Test]
    public async Task UnaOrdenRechazadaPorStockNoDejaNadaGuardado()
    {
        var alcanza = await CrearRepuestoAsync(5);
        var noAlcanza = await CrearRepuestoAsync(1);

        var respuesta = await CrearOrdenAsync(null, (alcanza, 2), (noAlcanza, 4));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await VerificarCantidadesAsync(alcanza, fisico: 5, reservada: 0, entregada: 0, disponible: 5);
    }

    [Test]
    public async Task AlIniciarLaOrdenLoReservadoPasaAEntregado()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var orden = await CrearOrdenOkAsync(await CrearTecnicoAsync(), (repuesto, 3));

        (await CambiarEstadoAsync(orden, "en_progreso")).EnsureSuccessStatusCode();

        Assert.That((await LeerOrdenAsync(orden)).GetProperty("repuestos")[0].GetProperty("estado").GetString(), Is.EqualTo("entregado"));
        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 0, entregada: 3, disponible: 7);
    }

    [Test]
    public async Task UnaOrdenPausadaOEnRevisionSigueConLosRepuestosEntregados()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var orden = await CrearOrdenOkAsync(await CrearTecnicoAsync(), (repuesto, 3));
        (await CambiarEstadoAsync(orden, "en_progreso")).EnsureSuccessStatusCode();
        (await CambiarEstadoAsync(orden, "pausado", "Falta material")).EnsureSuccessStatusCode();
        (await CambiarEstadoAsync(orden, "en_progreso")).EnsureSuccessStatusCode();
        (await CambiarEstadoAsync(orden, "en_revision")).EnsureSuccessStatusCode();
        (await CambiarEstadoAsync(orden, "pendiente", "Falta revisar")).EnsureSuccessStatusCode();

        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 0, entregada: 3, disponible: 7);
    }

    [Test]
    public async Task AlCerrarLaOrdenSeDescuentaElStockConUnEgresoQueQuedaRegistrado()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var orden = await CrearOrdenOkAsync(await CrearTecnicoAsync(), (repuesto, 3));
        foreach (var estado in new[] { "en_progreso", "en_revision" })
        {
            (await CambiarEstadoAsync(orden, estado)).EnsureSuccessStatusCode();
        }

        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 0, entregada: 3, disponible: 7);
        (await CambiarEstadoAsync(orden, "cerrado")).EnsureSuccessStatusCode();

        await VerificarCantidadesAsync(repuesto, fisico: 7, reservada: 0, entregada: 0, disponible: 7);
        Assert.That((await LeerOrdenAsync(orden)).GetProperty("repuestos")[0].GetProperty("estado").GetString(), Is.EqualTo("descontado"));
        var movimientos = await (await Cliente.GetAsync($"/api/v1/movimientos-stock?repuestoId={repuesto}")).LeerJsonAsync();
        var egreso = movimientos.GetProperty("elementos").EnumerateArray().Single();
        Assert.That(egreso.GetProperty("tipo").GetString(), Is.EqualTo("egreso"));
        Assert.That(egreso.GetProperty("cantidad").GetInt32(), Is.EqualTo(3));
        Assert.That(egreso.GetProperty("stockAnterior").GetInt32(), Is.EqualTo(10));
        Assert.That(egreso.GetProperty("stockPosterior").GetInt32(), Is.EqualTo(7));
        Assert.That(egreso.GetProperty("motivo").GetString(), Does.Contain($"orden de trabajo N° {orden}"));
    }

    [Test]
    public async Task ElStockNoBajaMientrasLaOrdenNoSeCierra()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var orden = await CrearOrdenOkAsync(await CrearTecnicoAsync(), (repuesto, 6));
        (await CambiarEstadoAsync(orden, "en_progreso")).EnsureSuccessStatusCode();
        (await CambiarEstadoAsync(orden, "en_revision")).EnsureSuccessStatusCode();

        var movimientos = await (await Cliente.GetAsync($"/api/v1/movimientos-stock?repuestoId={repuesto}")).LeerJsonAsync();

        Assert.That(movimientos.GetProperty("elementos").GetArrayLength(), Is.Zero);
        Assert.That((await LeerRepuestoAsync(repuesto)).GetProperty("cantidad").GetInt32(), Is.EqualTo(10));
    }

    [Test]
    public async Task EliminarLaOrdenLiberaLoReservado()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var orden = await CrearOrdenOkAsync(null, (repuesto, 4));

        (await CambiarEstadoAsync(orden, "eliminado", "Cargada por error")).EnsureSuccessStatusCode();

        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 0, entregada: 0, disponible: 10);
        Assert.That((await LeerOrdenAsync(orden)).GetProperty("repuestos").GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task EliminarUnaOrdenConRepuestosEntregadosLosVuelveALibresYNoDescuentaStock()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var orden = await CrearOrdenOkAsync(await CrearTecnicoAsync(), (repuesto, 4));
        (await CambiarEstadoAsync(orden, "en_progreso")).EnsureSuccessStatusCode();
        (await CambiarEstadoAsync(orden, "pausado", "Sin material")).EnsureSuccessStatusCode();

        (await CambiarEstadoAsync(orden, "eliminado", "Se cancela el trabajo")).EnsureSuccessStatusCode();

        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 0, entregada: 0, disponible: 10);
    }

    [Test]
    public async Task QuitarOCambiarLaCantidadLiberaLasUnidadesEnElMomento()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var orden = await CrearOrdenOkAsync(null, (repuesto, 6));

        (await PonerRepuestosAsync(orden, (repuesto, 2))).EnsureSuccessStatusCode();
        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 2, entregada: 0, disponible: 8);

        (await PonerRepuestosAsync(orden)).EnsureSuccessStatusCode();
        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 0, entregada: 0, disponible: 10);
    }

    [Test]
    public async Task SubirLaCantidadValidaElStockDisponibleYConservaLoYaReservadoPorLaMismaOrden()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var orden = await CrearOrdenOkAsync(null, (repuesto, 6));

        var hastaElTotal = await PonerRepuestosAsync(orden, (repuesto, 10));
        var demasiado = await PonerRepuestosAsync(orden, (repuesto, 11));

        Assert.That(hastaElTotal.StatusCode, Is.EqualTo(HttpStatusCode.OK), "las 6 ya reservadas por esta orden cuentan como disponibles");
        Assert.That(demasiado.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodigoAsync(demasiado), Is.EqualTo("STOCK_INSUFICIENTE"));
        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 10, entregada: 0, disponible: 0);
    }

    [Test]
    public async Task LoQueSeAgregaAUnaOrdenYaIniciadaQuedaEntregadoAlInstante()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var adicional = await CrearRepuestoAsync(5);
        var orden = await CrearOrdenOkAsync(await CrearTecnicoAsync(), (repuesto, 2));
        (await CambiarEstadoAsync(orden, "en_progreso")).EnsureSuccessStatusCode();

        (await PonerRepuestosAsync(orden, (repuesto, 3), (adicional, 2))).EnsureSuccessStatusCode();

        var lineas = (await LeerOrdenAsync(orden)).GetProperty("repuestos").EnumerateArray().ToList();
        Assert.That(lineas.Select(l => l.GetProperty("estado").GetString()), Is.All.EqualTo("entregado"));
        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 0, entregada: 3, disponible: 7);
        await VerificarCantidadesAsync(adicional, fisico: 5, reservada: 0, entregada: 2, disponible: 3);
    }

    [Test]
    public async Task UnaOrdenCerradaOEliminadaNoAdmiteCambiarSusRepuestos()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var eliminada = await CrearOrdenOkAsync(null, (repuesto, 1));
        (await CambiarEstadoAsync(eliminada, "eliminado", "Duplicada")).EnsureSuccessStatusCode();
        var cerrada = await CrearOrdenOkAsync(await CrearTecnicoAsync(), (repuesto, 1));
        foreach (var estado in new[] { "en_progreso", "en_revision", "cerrado" })
        {
            (await CambiarEstadoAsync(cerrada, estado)).EnsureSuccessStatusCode();
        }

        var sobreEliminada = await PonerRepuestosAsync(eliminada, (repuesto, 2));
        var sobreCerrada = await PonerRepuestosAsync(cerrada, (repuesto, 2));

        Assert.That(sobreEliminada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodigoAsync(sobreEliminada), Is.EqualTo("ORDEN_NO_MODIFICABLE"));
        Assert.That(sobreCerrada.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodigoAsync(sobreCerrada), Is.EqualTo("ORDEN_CERRADA"));
    }

    [Test]
    public async Task UnEgresoOAjusteManualNoPuedeDejarElStockPorDebajoDeLoComprometido()
    {
        var repuesto = await CrearRepuestoAsync(10);
        await CrearOrdenOkAsync(null, (repuesto, 8));

        var egresoDeMas = await Cliente.PostAsJsonAsync($"/api/v1/repuestos/{repuesto}/egresos", new { cantidad = 3, motivo = "Prueba" });
        var ajusteDeMas = await Cliente.PostAsJsonAsync($"/api/v1/repuestos/{repuesto}/ajustes", new { cantidadReal = 7, motivo = "Conteo" });
        var egresoPosible = await Cliente.PostAsJsonAsync($"/api/v1/repuestos/{repuesto}/egresos", new { cantidad = 2, motivo = "Prueba" });

        Assert.That(egresoDeMas.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await CodigoAsync(egresoDeMas), Is.EqualTo("STOCK_COMPROMETIDO"));
        Assert.That(await MensajeAsync(egresoDeMas), Does.Contain("8 unidades reservadas").Or.Contain("hay 8 unidades reservadas"));
        Assert.That(await CodigoAsync(ajusteDeMas), Is.EqualTo("STOCK_COMPROMETIDO"));
        Assert.That(egresoPosible.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task LasAlertasDeStockSiguenLaCantidadFisicaYNoLaDisponible()
    {
        var repuesto = await CrearRepuestoAsync(10, minimo: 4);

        await CrearOrdenOkAsync(null, (repuesto, 9));

        var detalle = await LeerRepuestoAsync(repuesto);
        Assert.That(detalle.GetProperty("estadoStock").GetString(), Is.EqualTo("normal"), "lo reservado sigue en la empresa");
        Assert.That(detalle.GetProperty("cantidadDisponible").GetInt32(), Is.EqualTo(1));
        var bajos = await (await Cliente.GetAsync("/api/v1/repuestos?bajoStock=true&tamanoPagina=100")).LeerJsonAsync();
        Assert.That(bajos.GetProperty("elementos").EnumerateArray().Select(r => r.GetProperty("id").GetInt64()), Does.Not.Contain(repuesto));
    }

    [Test]
    public async Task ElHistorialDeRepuestosGuardaCadaCambioEnOrden()
    {
        var repuesto = await CrearRepuestoAsync(10);
        var orden = await CrearOrdenOkAsync(await CrearTecnicoAsync(), (repuesto, 4));
        (await PonerRepuestosAsync(orden, (repuesto, 3))).EnsureSuccessStatusCode();
        (await CambiarEstadoAsync(orden, "en_progreso")).EnsureSuccessStatusCode();
        foreach (var estado in new[] { "en_revision", "cerrado" })
        {
            (await CambiarEstadoAsync(orden, estado)).EnsureSuccessStatusCode();
        }

        var historial = (await LeerOrdenAsync(orden)).GetProperty("historialRepuestos").EnumerateArray().ToList();

        Assert.That(historial.Select(h => h.GetProperty("accion").GetString()), Is.EqualTo(new[] { "reservar", "modificar", "entregar", "descontar" }));
        Assert.That(historial.Select(h => h.GetProperty("estadoNuevo").GetString()), Is.EqualTo(new[] { "reservado", "reservado", "entregado", "descontado" }));
        Assert.That(historial[1].GetProperty("cantidadAnterior").GetInt32(), Is.EqualTo(4));
        Assert.That(historial[1].GetProperty("cantidadNueva").GetInt32(), Is.EqualTo(3));
        Assert.That(historial.Select(h => h.GetProperty("fechaHora").GetDateTime()), Is.Ordered);
    }

    [Test]
    public async Task UnRepuestoRepetidoOUnaCantidadInvalidaResponde400()
    {
        var repuesto = await CrearRepuestoAsync(10);

        var repetido = await CrearOrdenAsync(null, (repuesto, 1), (repuesto, 2));
        var enCero = await CrearOrdenAsync(null, (repuesto, 0));
        var inexistente = await CrearOrdenAsync(null, (long.MaxValue, 1));

        Assert.That(repetido.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(enCero.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(inexistente.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CincoOrdenesSimultaneasNoPuedenReservarMasQueElStock()
    {
        var repuesto = await CrearRepuestoAsync(10);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => CrearOrdenAsync(null, (repuesto, 4))));

        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.Created), Is.EqualTo(2));
        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict), Is.EqualTo(3));
        await VerificarCantidadesAsync(repuesto, fisico: 10, reservada: 8, entregada: 0, disponible: 2);
    }

    [Test]
    public async Task ElListadoDeRepuestosSePuedeOrdenarPorReservadoEntregadoYDisponible()
    {
        foreach (var columna in new[] { "cantidadReservada", "cantidadEntregada", "cantidadDisponible" })
        {
            var respuesta = await Cliente.GetAsync($"/api/v1/repuestos?ordenarPor={columna}&direccionOrden=desc&tamanoPagina=5");

            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), columna);
        }
    }
}
