using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Controladores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SOTRA_BACKEND.Contexto;

namespace Api.Pruebas.Caracterizacion;

public partial class AnaliticaAvanzadaPruebas
{
    private const string RutaConsumoRepuestos = "/api/v1/reportes/consumo-repuestos";

    private async Task<long> CrearOrdenConRepuestoAsync(JsonElement activo, long repuestoId, int cantidad)
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Consumo de prueba",
            fechaPrevistaEjecucion = DateOnly.FromDateTime(DateTime.UtcNow),
            repuestos = new[] { new { repuestoId, cantidad } },
        });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created), await respuesta.Content.ReadAsStringAsync());
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    [Test]
    public async Task ConsumoSumaUnidadesDescontadasNoReservasNiOrdenes()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var repuestoId = await CrearRepuestoParaModeloAsync();
        var primera = await CrearOrdenConRepuestoAsync(activo, repuestoId, 2);
        var segunda = await CrearOrdenConRepuestoAsync(activo, repuestoId, 8);
        await CrearOrdenConRepuestoAsync(activo, repuestoId, 3); // Reservada: no es consumo.
        await CerrarAsync(primera);
        await CerrarAsync(segunda);

        var ruta = $"{RutaConsumoRepuestos}?activoId={activo.GetProperty("id").GetInt64()}";
        var datos = await LeerAsync(ruta);
        Assert.That(datos.GetProperty("totalUnidades").GetInt64(), Is.EqualTo(10));
        var fila = datos.GetProperty("repuestos").EnumerateArray().Single();
        Assert.That(fila.GetProperty("repuestoId").GetInt64(), Is.EqualTo(repuestoId));
        Assert.That(fila.GetProperty("unidades").GetInt64(), Is.EqualTo(10));
        Assert.That(fila.GetProperty("ordenes").GetInt32(), Is.EqualTo(2));
        Assert.That(datos.ToString(), Does.Not.Contain("usuario"));

        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        await alcance.ServiceProvider.GetRequiredService<SotraContext>().Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE operativo.movimiento_stock SET fecha = {DateTime.UtcNow.AddDays(-10)}
            WHERE id_movimiento = (SELECT id_movimiento FROM operativo.ot_repuesto WHERE id_orden = {primera})
            """);
        datos = await LeerAsync(ruta + $"&fechaDesde={Hoy}&fechaHasta={Hoy}");
        Assert.That(datos.GetProperty("totalUnidades").GetInt64(), Is.EqualTo(8));
    }

    [TestCase("fechaDesde=2026-02-01&fechaHasta=2026-01-01", "RANGO_FECHAS_INVALIDO")]
    [TestCase("fechaHasta=9999-12-31", "RANGO_FECHAS_INVALIDO")]
    [TestCase("clienteId=0", "FILTROS_INVALIDOS")]
    public async Task ConsumoRechazaFiltrosInvalidos(string consulta, string codigo)
    {
        var respuesta = await Cliente.GetAsync(RutaConsumoRepuestos + "?" + consulta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo(codigo));
    }

    [Test]
    public void ConsumoExigePermisoDeReportes() => Assert.That(
        typeof(ConsumoDeRepuestosController).GetMethod("Obtener")!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Policy, Is.EqualTo(Politicas.ReporteConsultar));
}
