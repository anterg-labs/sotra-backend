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
    private const string RutaRepuestosPorModelo = "/api/v1/reportes/repuestos-por-modelo";

    private async Task<long> CrearRepuestoParaModeloAsync()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "RPM-" + DatosDePrueba.Sufijo(),
            nombre = "Repuesto A12",
            cantidad = 20,
            cantidadMinima = 1,
        });
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
    }

    private static async Task VincularRepuestoAsync(long ordenId, long repuestoId, string estado)
    {
        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        await alcance.ServiceProvider.GetRequiredService<SotraContext>().Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO operativo.ot_repuesto (id_orden, id_repuesto, cantidad, estado) VALUES ({ordenId}, {repuestoId}, 2, {estado})");
    }

    [Test]
    public async Task RepuestosPorModeloCuentaOrdenesNoUnidadesNiPreventivas()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var primerRepuesto = await CrearRepuestoParaModeloAsync();
        var segundoRepuesto = await CrearRepuestoParaModeloAsync();
        var ordenUno = await CrearOrdenAsync(activo, "correctivo");
        var ordenDos = await CrearOrdenAsync(activo, "correctivo");
        var preventiva = await CrearOrdenAsync(activo, "preventivo");
        await VincularRepuestoAsync(ordenUno, primerRepuesto, "reservado");
        await VincularRepuestoAsync(ordenDos, primerRepuesto, "entregado");
        await VincularRepuestoAsync(ordenDos, segundoRepuesto, "descontado");
        await VincularRepuestoAsync(preventiva, segundoRepuesto, "descontado");

        var datos = await LeerAsync($"{RutaRepuestosPorModelo}?clienteId={clienteId}");
        var modelo = datos.GetProperty("modelos").EnumerateArray().Single();
        var repuestos = modelo.GetProperty("repuestos").EnumerateArray().ToList();
        Assert.That(repuestos, Has.Count.EqualTo(2));
        Assert.That(repuestos[0].GetProperty("ordenesAsociadas").GetInt32(), Is.EqualTo(2));
        Assert.That(repuestos[0].GetProperty("ordenesSuministradas").GetInt32(), Is.EqualTo(1));
        Assert.That(repuestos[1].GetProperty("ordenesAsociadas").GetInt32(), Is.EqualTo(1));
        Assert.That(repuestos[1].GetProperty("ordenesSuministradas").GetInt32(), Is.EqualTo(1));
        Assert.That(repuestos[0].GetProperty("repuestoId").GetInt64(), Is.EqualTo(primerRepuesto));

        var filtrado = await LeerAsync($"{RutaRepuestosPorModelo}?activoId={activo.GetProperty("id").GetInt64()}&fechaDesde={Hoy}&fechaHasta={Hoy}");
        Assert.That(filtrado.GetProperty("modelos").GetArrayLength(), Is.EqualTo(1));
    }

    [Test]
    public async Task RepuestosPorModeloExcluyeLiberadosYDistingueSinDatos()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var orden = await CrearOrdenAsync(activo, "correctivo");
        await VincularRepuestoAsync(orden, await CrearRepuestoParaModeloAsync(), "liberado");
        var datos = await LeerAsync($"{RutaRepuestosPorModelo}?activoId={activo.GetProperty("id").GetInt64()}");
        Assert.That(datos.GetProperty("modelos").GetArrayLength(), Is.Zero);
    }

    [Test]
    public async Task RepuestosPorModeloNoMezclaModelosNiClientes()
    {
        var primerActivo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = primerActivo.GetProperty("clienteId").GetInt64();
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var modelo = await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipo.GetProperty("id").GetInt64());
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/activos",
            DatosDePrueba.ActivoNuevo(clienteId, modelo.GetProperty("id").GetInt64()));
        respuesta.EnsureSuccessStatusCode();
        var segundoActivo = await respuesta.LeerJsonAsync();
        var repuesto = await CrearRepuestoParaModeloAsync();
        await VincularRepuestoAsync(await CrearOrdenAsync(primerActivo, "correctivo"), repuesto, "reservado");
        await VincularRepuestoAsync(await CrearOrdenAsync(segundoActivo, "correctivo"), repuesto, "entregado");

        var datos = await LeerAsync($"{RutaRepuestosPorModelo}?clienteId={clienteId}");
        var modelos = datos.GetProperty("modelos").EnumerateArray().ToList();
        Assert.That(modelos, Has.Count.EqualTo(2));
        Assert.That(modelos.Select(elemento => elemento.GetProperty("modeloId").GetInt64()).Distinct().Count(), Is.EqualTo(2));
        Assert.That(modelos.All(elemento => elemento.GetProperty("repuestos")[0].GetProperty("ordenesAsociadas").GetInt32() == 1), Is.True);
        var soloSegundo = await LeerAsync($"{RutaRepuestosPorModelo}?clienteId={clienteId}&activoId={segundoActivo.GetProperty("id").GetInt64()}");
        Assert.That(soloSegundo.GetProperty("modelos").GetArrayLength(), Is.EqualTo(1));
        Assert.That(soloSegundo.GetProperty("modelos")[0].GetProperty("modeloId").GetInt64(), Is.EqualTo(modelo.GetProperty("id").GetInt64()));
    }

    [TestCase("fechaDesde=2026-02-01&fechaHasta=2026-01-01", "RANGO_FECHAS_INVALIDO")]
    [TestCase("fechaHasta=9999-12-31", "RANGO_FECHAS_INVALIDO")]
    [TestCase("clienteId=0", "FILTROS_INVALIDOS")]
    [TestCase("activoId=-1", "FILTROS_INVALIDOS")]
    public async Task RepuestosPorModeloRechazaFiltrosInvalidos(string consulta, string codigo)
    {
        var respuesta = await Cliente.GetAsync(RutaRepuestosPorModelo + "?" + consulta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo(codigo));
    }

    [Test]
    public void RepuestosPorModeloExigePermisoDeReportes() => Assert.That(
        typeof(RepuestosPorModeloController).GetMethod("Obtener")!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Policy, Is.EqualTo(Politicas.ReporteConsultar));
}
