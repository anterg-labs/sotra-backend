using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class StockPruebas : PruebaBase
{
    private async Task<long> CrearRepuestoAsync(int cantidad, int minimo)
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "STK-" + DatosDePrueba.Sufijo(),
            nombre = "Repuesto de stock",
            ubicacion = "Depósito A",
            cantidad,
            cantidadMinima = minimo,
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private async Task<JsonElement> ObtenerAsync(long id)
    {
        return await (await Cliente.GetAsync($"/api/v1/repuestos/{id}")).LeerJsonAsync();
    }

    private Task<HttpResponseMessage> IngresarAsync(long id, int cantidad)
    {
        return Cliente.PostAsJsonAsync($"/api/v1/repuestos/{id}/ingresos", new
        {
            cantidad,
            fecha = DateOnly.FromDateTime(DateTime.UtcNow),
            motivo = "Compra",
            ubicacion = "Depósito A",
        });
    }

    private Task<HttpResponseMessage> EgresarAsync(long id, int cantidad)
    {
        return Cliente.PostAsJsonAsync($"/api/v1/repuestos/{id}/egresos", new { cantidad, motivo = "Uso en taller" });
    }

    private async Task<(int Bajo, int SinStock, int Total)> LeerAlertasAsync()
    {
        var cuerpo = await (await Cliente.GetAsync("/api/v1/repuestos/alertas-stock")).LeerJsonAsync();
        return (cuerpo.GetProperty("bajo").GetInt32(), cuerpo.GetProperty("sinStock").GetInt32(),
            cuerpo.GetProperty("total").GetInt32());
    }

    private static async Task<string?> LeerCodigoAsync(HttpResponseMessage respuesta)
    {
        return (await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString();
    }

    [TestCase(10, 5, "normal")]
    [TestCase(6, 5, "normal")]
    [TestCase(5, 5, "bajo")]
    [TestCase(3, 5, "bajo")]
    [TestCase(0, 5, "sin_stock")]
    [TestCase(0, 0, "sin_stock")]
    [TestCase(4, 0, "normal")]
    public async Task ElServidorCalculaElEstadoDeStock(int cantidad, int minimo, string esperado)
    {
        var id = await CrearRepuestoAsync(cantidad, minimo);

        var repuesto = await ObtenerAsync(id);

        Assert.That(repuesto.GetProperty("estadoStock").GetString(), Is.EqualTo(esperado));
    }

    [Test]
    public async Task ElFiltroPorEstadoDeStockYElContadorCoincidenConElListado()
    {
        var antes = await LeerAlertasAsync();
        var idBajo = await CrearRepuestoAsync(2, 5);
        var idSin = await CrearRepuestoAsync(0, 5);
        var idNormal = await CrearRepuestoAsync(50, 5);

        var despues = await LeerAlertasAsync();
        Assert.That(despues.Bajo, Is.EqualTo(antes.Bajo + 1));
        Assert.That(despues.SinStock, Is.EqualTo(antes.SinStock + 1));
        Assert.That(despues.Total, Is.EqualTo(despues.Bajo + despues.SinStock));

        var bajos = await (await Cliente.GetAsync("/api/v1/repuestos?estadoStock=bajo&estado=activo&tamanoPagina=100")).LeerJsonAsync();
        Assert.That(bajos.GetProperty("totalElementos").GetInt32(), Is.EqualTo(despues.Bajo));
        var idsBajos = bajos.GetProperty("elementos").EnumerateArray().Select(e => e.GetProperty("id").GetInt64()).ToList();
        Assert.That(idsBajos, Does.Contain(idBajo));
        Assert.That(idsBajos, Does.Not.Contain(idSin));
        Assert.That(idsBajos, Does.Not.Contain(idNormal));

        var sinStock = await (await Cliente.GetAsync("/api/v1/repuestos?estadoStock=sin_stock&estado=activo")).LeerJsonAsync();
        Assert.That(sinStock.GetProperty("totalElementos").GetInt32(), Is.EqualTo(despues.SinStock));
    }

    [Test]
    public async Task ElOrdenPorCriticidadPoneSinStockAntesQueBajoYBajoAntesQueNormal()
    {
        await CrearRepuestoAsync(50, 5);
        await CrearRepuestoAsync(1, 5);
        await CrearRepuestoAsync(0, 5);

        var pagina = await (await Cliente.GetAsync("/api/v1/repuestos?orden=criticidad&estado=activo&tamanoPagina=100")).LeerJsonAsync();

        var rangos = pagina.GetProperty("elementos").EnumerateArray()
            .Select(e => e.GetProperty("estadoStock").GetString() switch { "sin_stock" => 0, "bajo" => 1, _ => 2 })
            .ToList();
        Assert.That(rangos, Is.Ordered);
    }

    [Test]
    public async Task UnEstadoDeStockDesconocidoResponde400()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/repuestos?estadoStock=critico");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("ESTADO_STOCK_INVALIDO"));
    }

    [Test]
    public async Task UnRepuestoInactivoNoGeneraAlertas()
    {
        var id = await CrearRepuestoAsync(0, 5);
        var conAlerta = await LeerAlertasAsync();

        (await Cliente.PatchAsJsonAsync($"/api/v1/repuestos/{id}", new { estado = "inactivo" })).EnsureSuccessStatusCode();

        Assert.That((await LeerAlertasAsync()).SinStock, Is.EqualTo(conAlerta.SinStock - 1));
    }

    [Test]
    public async Task IngresarStockHastaSuperarElMinimoQuitaElAvisoYBajaElContador()
    {
        var id = await CrearRepuestoAsync(1, 5);
        var antes = await LeerAlertasAsync();

        var respuesta = await IngresarAsync(id, 10);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var movimiento = await respuesta.LeerJsonAsync();
        Assert.That(movimiento.GetProperty("stockAnterior").GetInt32(), Is.EqualTo(1));
        Assert.That(movimiento.GetProperty("stockPosterior").GetInt32(), Is.EqualTo(11));
        Assert.That((await ObtenerAsync(id)).GetProperty("estadoStock").GetString(), Is.EqualTo("normal"));
        Assert.That((await LeerAlertasAsync()).Bajo, Is.EqualTo(antes.Bajo - 1));
    }

    [Test]
    public async Task ElEgresoDescuentaElStockYRegistraElMovimiento()
    {
        var id = await CrearRepuestoAsync(10, 2);

        var respuesta = await EgresarAsync(id, 4);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var movimiento = await respuesta.LeerJsonAsync();
        Assert.That(movimiento.GetProperty("tipo").GetString(), Is.EqualTo("egreso"));
        Assert.That(movimiento.GetProperty("stockAnterior").GetInt32(), Is.EqualTo(10));
        Assert.That(movimiento.GetProperty("stockPosterior").GetInt32(), Is.EqualTo(6));
        Assert.That((await ObtenerAsync(id)).GetProperty("cantidad").GetInt32(), Is.EqualTo(6));
    }

    [Test]
    public async Task UnEgresoMayorAlStockResponde409SinCambiarNada()
    {
        var id = await CrearRepuestoAsync(3, 1);

        var respuesta = await EgresarAsync(id, 4);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("STOCK_INSUFICIENTE"));
        Assert.That((await ObtenerAsync(id)).GetProperty("cantidad").GetInt32(), Is.EqualTo(3));
    }

    [TestCase(0)]
    [TestCase(-2)]
    public async Task UnEgresoConCantidadNoPositivaResponde400(int cantidad)
    {
        var id = await CrearRepuestoAsync(5, 1);

        var respuesta = await EgresarAsync(id, cantidad);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("CANTIDAD_INVALIDA"));
    }

    [Test]
    public async Task UnEgresoSinMotivoResponde400()
    {
        var id = await CrearRepuestoAsync(5, 1);

        var respuesta = await Cliente.PostAsJsonAsync($"/api/v1/repuestos/{id}/egresos", new { cantidad = 1, motivo = "  " });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await LeerCodigoAsync(respuesta), Is.EqualTo("DATOS_OBLIGATORIOS"));
    }

    [Test]
    public async Task ElAjusteFijaElStockRealYGuardaLaDiferencia()
    {
        var id = await CrearRepuestoAsync(10, 2);

        var respuesta = await Cliente.PostAsJsonAsync($"/api/v1/repuestos/{id}/ajustes",
            new { cantidadReal = 7, motivo = "Conteo físico" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var movimiento = await respuesta.LeerJsonAsync();
        Assert.That(movimiento.GetProperty("tipo").GetString(), Is.EqualTo("ajuste"));
        Assert.That(movimiento.GetProperty("cantidad").GetInt32(), Is.EqualTo(-3));
        Assert.That(movimiento.GetProperty("stockPosterior").GetInt32(), Is.EqualTo(7));
        Assert.That((await ObtenerAsync(id)).GetProperty("cantidad").GetInt32(), Is.EqualTo(7));
    }

    [Test]
    public async Task UnAjusteNegativoResponde400()
    {
        var id = await CrearRepuestoAsync(10, 2);

        var respuesta = await Cliente.PostAsJsonAsync($"/api/v1/repuestos/{id}/ajustes",
            new { cantidadReal = -1, motivo = "Conteo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task NoSePuedeMoverElStockDeUnRepuestoInactivoNiInexistente()
    {
        var id = await CrearRepuestoAsync(5, 1);
        (await Cliente.PatchAsJsonAsync($"/api/v1/repuestos/{id}", new { estado = "inactivo" })).EnsureSuccessStatusCode();

        var inactivo = await EgresarAsync(id, 1);
        var inexistente = await EgresarAsync(long.MaxValue, 1);

        Assert.That(inactivo.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(await LeerCodigoAsync(inactivo), Is.EqualTo("REPUESTO_INACTIVO"));
        Assert.That(inexistente.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task EditarElMinimoPorEncimaDeLaCantidadPasaElRepuestoAStockBajo()
    {
        var id = await CrearRepuestoAsync(10, 2);

        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/repuestos/{id}/stock-minimo", new { cantidadMinima = 12 });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var repuesto = await respuesta.LeerJsonAsync();
        Assert.That(repuesto.GetProperty("cantidadMinima").GetInt32(), Is.EqualTo(12));
        Assert.That(repuesto.GetProperty("estadoStock").GetString(), Is.EqualTo("bajo"));
    }

    [TestCase("-1")]
    [TestCase("2.5")]
    [TestCase("\"abc\"")]
    public async Task UnMinimoNegativoDecimalONoNumericoSeRechaza(string valor)
    {
        var id = await CrearRepuestoAsync(10, 2);
        var contenido = new StringContent("{\"cantidadMinima\":" + valor + "}", Encoding.UTF8, "application/json");

        var respuesta = await Cliente.PatchAsync($"/api/v1/repuestos/{id}/stock-minimo", contenido);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await ObtenerAsync(id)).GetProperty("cantidadMinima").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task EditarElMinimoDeUnRepuestoInexistenteResponde404()
    {
        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/repuestos/{long.MaxValue}/stock-minimo", new { cantidadMinima = 3 });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task VeinteIngresosSimultaneosSumanSinPerderMovimientos()
    {
        var id = await CrearRepuestoAsync(0, 1);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => IngresarAsync(id, 1)));

        Assert.That(respuestas.Select(r => r.StatusCode), Has.All.EqualTo(HttpStatusCode.Created));
        Assert.That((await ObtenerAsync(id)).GetProperty("cantidad").GetInt32(), Is.EqualTo(20));
        var movimientos = await (await Cliente.GetAsync($"/api/v1/movimientos-stock?repuestoId={id}&tamanoPagina=100")).LeerJsonAsync();
        Assert.That(movimientos.GetProperty("totalElementos").GetInt32(), Is.EqualTo(20));
    }

    [Test]
    public async Task DiezEgresosSimultaneosSobreCincoUnidadesNuncaDejanElStockNegativo()
    {
        var id = await CrearRepuestoAsync(5, 1);

        var respuestas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => EgresarAsync(id, 1)));

        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.Created), Is.EqualTo(5));
        Assert.That(respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict), Is.EqualTo(5));
        Assert.That((await ObtenerAsync(id)).GetProperty("cantidad").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task ElDashboardUsaLaMismaReglaQueElListado()
    {
        var idSin = await CrearRepuestoAsync(0, 0);
        var idNormal = await CrearRepuestoAsync(3, 0);

        var indicadores = await (await Cliente.GetAsync("/api/v1/reportes/indicadores")).LeerJsonAsync();

        var ids = indicadores.GetProperty("repuestosBajoStock").EnumerateArray()
            .Select(e => e.GetProperty("id").GetInt64()).ToList();
        Assert.That(ids, Does.Contain(idSin));
        Assert.That(ids, Does.Not.Contain(idNormal));
    }

    [Test]
    public async Task ModificarUnRepuestoConPutNoCambiaLaCantidad()
    {
        var id = await CrearRepuestoAsync(10, 2);

        var respuesta = await Cliente.PutAsJsonAsync($"/api/v1/repuestos/{id}", new
        {
            codigo = "STK-" + DatosDePrueba.Sufijo(),
            nombre = "Nombre nuevo",
            cantidad = 999,
            cantidadMinima = 4,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var repuesto = await respuesta.LeerJsonAsync();
        Assert.That(repuesto.GetProperty("nombre").GetString(), Is.EqualTo("Nombre nuevo"));
        Assert.That(repuesto.GetProperty("cantidadMinima").GetInt32(), Is.EqualTo(4));
        Assert.That(repuesto.GetProperty("cantidad").GetInt32(), Is.EqualTo(10));
    }

    [Test]
    public async Task UnRepuestoDadoDeBajaSePuedeReactivarYVuelveAMoverStock()
    {
        var id = await CrearRepuestoAsync(3, 1);
        (await Cliente.PatchAsJsonAsync($"/api/v1/repuestos/{id}", new { estado = "inactivo" })).EnsureSuccessStatusCode();
        Assert.That((await EgresarAsync(id, 1)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        var reactivado = await Cliente.PatchAsJsonAsync($"/api/v1/repuestos/{id}", new { estado = "activo" });

        Assert.That(reactivado.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await reactivado.LeerJsonAsync()).GetProperty("estado").GetString(), Is.EqualTo("activo"));
        Assert.That((await EgresarAsync(id, 1)).StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task CambiarElEstadoConUnValorDesconocidoOEnUnRepuestoInexistenteFalla()
    {
        var id = await CrearRepuestoAsync(3, 1);

        var invalido = await Cliente.PatchAsJsonAsync($"/api/v1/repuestos/{id}", new { estado = "pausado" });
        var inexistente = await Cliente.PatchAsJsonAsync($"/api/v1/repuestos/{long.MaxValue}", new { estado = "activo" });

        Assert.That(invalido.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(inexistente.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
