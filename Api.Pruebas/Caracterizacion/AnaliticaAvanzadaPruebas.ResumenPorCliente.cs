using System.Net;
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
    private const string RutaResumenPorCliente = "/api/v1/reportes/resumen-por-cliente";

    private static async Task CambiarFechasResumenAsync(long ordenId, DateTime creada, DateTime? cierre = null)
    {
        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        await alcance.ServiceProvider.GetRequiredService<SotraContext>().Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE operativo.orden_trabajo SET fecha_creada = {creada}, fecha_cierre = {cierre} WHERE id_orden = {ordenId}");
    }

    [Test]
    public async Task ResumenPorClienteAgrupaHorasYReincidenciasConCriterioExistente()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var primera = await CrearOrdenAsync(activo, "correctivo");
        await CerrarAsync(primera);
        var segunda = await CrearOrdenAsync(activo, "correctivo");
        await CrearOrdenAsync(activo, "preventivo");
        var baseUtc = DateTime.UtcNow.Date;
        await CambiarFechasResumenAsync(primera, baseUtc.AddDays(-10), baseUtc.AddDays(-8));
        await CambiarFechasResumenAsync(segunda, baseUtc.AddDays(-5));

        var datos = await LeerAsync($"{RutaResumenPorCliente}?clienteId={clienteId}&dias=90");
        Assert.That(datos.GetProperty("diasReincidencia").GetInt32(), Is.EqualTo(90));
        var fila = datos.GetProperty("clientes").EnumerateArray().Single();
        Assert.That(fila.GetProperty("clienteId").GetInt64(), Is.EqualTo(clienteId));
        Assert.That(fila.GetProperty("ordenes").GetInt32(), Is.EqualTo(3));
        Assert.That(fila.GetProperty("correctivas").GetInt32(), Is.EqualTo(2));
        Assert.That(fila.GetProperty("correctivasCerradas").GetInt32(), Is.EqualTo(1));
        Assert.That(fila.GetProperty("horasParada").GetDouble(), Is.EqualTo(48).Within(0.01));
        Assert.That(fila.GetProperty("reincidencias").GetInt32(), Is.EqualTo(1));
        Assert.That(datos.ToString(), Does.Not.Contain("usuario"));
        var existente = await LeerAsync($"/api/v1/reportes/reincidencias?clienteId={clienteId}&dias=90");
        Assert.That(existente.GetProperty("elementos")[0].GetProperty("reincidencias").GetInt32(), Is.EqualTo(1));

        var fecha = DateOnly.FromDateTime(baseUtc.AddDays(-5)).ToString("yyyy-MM-dd");
        var acotado = await LeerAsync($"{RutaResumenPorCliente}?clienteId={clienteId}&fechaDesde={fecha}");
        var filaAcotada = acotado.GetProperty("clientes")[0];
        Assert.That(filaAcotada.GetProperty("correctivasCerradas").GetInt32(), Is.Zero);
        Assert.That(filaAcotada.GetProperty("horasParada").GetDouble(), Is.Zero);
        Assert.That(filaAcotada.GetProperty("reincidencias").GetInt32(), Is.Zero);
        var ventanaCorta = await LeerAsync($"{RutaResumenPorCliente}?clienteId={clienteId}&dias=1");
        Assert.That(ventanaCorta.GetProperty("clientes")[0].GetProperty("reincidencias").GetInt32(), Is.Zero);
    }

    [Test]
    public async Task ResumenPorClienteSeparaEmpresasYAceptaVacio()
    {
        var primero = await DatosDePrueba.CrearActivoAsync(Cliente);
        var segundo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var primerCliente = primero.GetProperty("clienteId").GetInt64();
        var segundoCliente = segundo.GetProperty("clienteId").GetInt64();
        await CrearOrdenAsync(primero, "preventivo");
        await CrearOrdenAsync(segundo, "correctivo");
        var delPrimero = await LeerAsync($"{RutaResumenPorCliente}?clienteId={primerCliente}");
        Assert.That(delPrimero.GetProperty("clientes").GetArrayLength(), Is.EqualTo(1));
        Assert.That(delPrimero.GetProperty("clientes")[0].GetProperty("correctivas").GetInt32(), Is.Zero);
        var delSegundo = await LeerAsync($"{RutaResumenPorCliente}?clienteId={segundoCliente}");
        Assert.That(delSegundo.GetProperty("clientes")[0].GetProperty("correctivas").GetInt32(), Is.EqualTo(1));
        var sinCoincidencia = await LeerAsync($"{RutaResumenPorCliente}?clienteId={primerCliente}&activoId={segundo.GetProperty("id").GetInt64()}");
        Assert.That(sinCoincidencia.GetProperty("clientes").GetArrayLength(), Is.Zero);
    }

    [TestCase("fechaDesde=2026-02-01&fechaHasta=2026-01-01", "RANGO_FECHAS_INVALIDO")]
    [TestCase("fechaHasta=9999-12-31", "RANGO_FECHAS_INVALIDO")]
    [TestCase("clienteId=0", "FILTROS_INVALIDOS")]
    [TestCase("activoId=-1", "FILTROS_INVALIDOS")]
    [TestCase("dias=0", "PARAMETRO_INVALIDO")]
    [TestCase("dias=3651", "PARAMETRO_INVALIDO")]
    public async Task ResumenPorClienteRechazaFiltrosInvalidos(string consulta, string codigo)
    {
        var respuesta = await Cliente.GetAsync(RutaResumenPorCliente + "?" + consulta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo(codigo));
    }

    [TestCase("fechaDesde=ayer")]
    [TestCase("clienteId=1%20OR%201=1")]
    public async Task ResumenPorClienteRechazaTiposMalformados(string consulta) =>
        Assert.That((await Cliente.GetAsync(RutaResumenPorCliente + "?" + consulta)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

    [Test]
    public void ResumenPorClienteExigePermisoDeReportes() => Assert.That(
        typeof(ResumenPorClienteController).GetMethod("Obtener")!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Policy, Is.EqualTo(Politicas.ReporteConsultar));
}
