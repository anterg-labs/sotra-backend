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
    private const string RutaEstacionalidad = "/api/v1/reportes/estacionalidad";

    private async Task CrearOrdenEstacionalAsync(JsonElement activo, string tipo, DateTime fecha)
    {
        var id = await CrearOrdenAsync(activo, tipo);
        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        await alcance.ServiceProvider.GetRequiredService<SotraContext>().Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE operativo.orden_trabajo SET fecha_creada = {fecha} WHERE id_orden = {id}");
    }

    [Test]
    public async Task EstacionalidadAcumulaAniosPorTipoCompletaCerosYConservaTotales()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        await CrearOrdenEstacionalAsync(activo, "correctivo", new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        await CrearOrdenEstacionalAsync(activo, "correctivo", new DateTime(2026, 1, 31, 23, 59, 59, DateTimeKind.Utc));
        await CrearOrdenEstacionalAsync(activo, "correctivo", new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
        await CrearOrdenEstacionalAsync(activo, "preventivo", new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var modelo = await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipo.GetProperty("id").GetInt64());
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(clienteId, modelo.GetProperty("id").GetInt64()));
        respuesta.EnsureSuccessStatusCode();
        var otroActivo = await respuesta.LeerJsonAsync();
        await CrearOrdenEstacionalAsync(otroActivo, "correctivo", new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc));

        var datos = await LeerAsync($"{RutaEstacionalidad}?clienteId={clienteId}&fechaDesde=2025-01-01&fechaHasta=2026-12-31");
        Assert.That(datos.GetProperty("total").GetInt32(), Is.EqualTo(4));
        var tipos = datos.GetProperty("tipos").EnumerateArray().ToList();
        Assert.That(tipos, Has.Count.EqualTo(2));
        Assert.That(tipos.Sum(t => t.GetProperty("total").GetInt32()), Is.EqualTo(4));
        foreach (var fila in tipos)
        {
            var meses = fila.GetProperty("meses").EnumerateArray().ToList();
            Assert.That(meses.Select(m => m.GetProperty("mes").GetInt32()), Is.EqualTo(Enumerable.Range(1, 12)));
            Assert.That(meses.Sum(m => m.GetProperty("correctivas").GetInt32()), Is.EqualTo(fila.GetProperty("total").GetInt32()));
            Assert.That(meses[2].GetProperty("correctivas").GetInt32(), Is.Zero);
        }
        var totales = datos.GetProperty("meses");
        Assert.That(totales[0].GetProperty("correctivas").GetInt32(), Is.EqualTo(2));
        Assert.That(totales[1].GetProperty("correctivas").GetInt32(), Is.EqualTo(1));
        Assert.That(totales[11].GetProperty("correctivas").GetInt32(), Is.EqualTo(1));

        var porActivo = await LeerAsync($"{RutaEstacionalidad}?activoId={activo.GetProperty("id").GetInt64()}&fechaDesde=2026-01-01&fechaHasta=2026-01-31");
        Assert.That(porActivo.GetProperty("total").GetInt32(), Is.EqualTo(1));
        Assert.That(porActivo.GetProperty("tipos").GetArrayLength(), Is.EqualTo(1));
        var hasta = await LeerAsync($"{RutaEstacionalidad}?clienteId={clienteId}&fechaHasta=2025-12-31");
        Assert.That(hasta.GetProperty("total").GetInt32(), Is.EqualTo(1));
        var sinFechas = await LeerAsync($"{RutaEstacionalidad}?clienteId={clienteId}");
        Assert.That(sinFechas.GetProperty("total").GetInt32(), Is.EqualTo(4));
        Assert.That(sinFechas.GetProperty("fechaDesde").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task EstacionalidadVaciaNoInventaTiposYNoMezclaClientes()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenEstacionalAsync(activo, "correctivo", DateTime.UtcNow);
        var ajeno = await DatosDePrueba.CrearActivoAsync(Cliente);
        var datos = await LeerAsync($"{RutaEstacionalidad}?activoId={activo.GetProperty("id").GetInt64()}&clienteId={ajeno.GetProperty("clienteId").GetInt64()}");
        Assert.That(datos.GetProperty("total").GetInt32(), Is.Zero);
        Assert.That(datos.GetProperty("tipos").GetArrayLength(), Is.Zero);
        Assert.That(datos.GetProperty("meses").GetArrayLength(), Is.EqualTo(12));
        Assert.That(datos.GetProperty("meses").EnumerateArray().All(m => m.GetProperty("correctivas").GetInt32() == 0), Is.True);
    }

    [TestCase("fechaDesde=2026-02-01&fechaHasta=2026-01-01", "RANGO_FECHAS_INVALIDO")]
    [TestCase("fechaHasta=9999-12-31", "RANGO_FECHAS_INVALIDO")]
    [TestCase("clienteId=0", "FILTROS_INVALIDOS")]
    [TestCase("activoId=-1", "FILTROS_INVALIDOS")]
    public async Task EstacionalidadRechazaFiltrosInvalidos(string consulta, string codigo)
    {
        var respuesta = await Cliente.GetAsync(RutaEstacionalidad + "?" + consulta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo(codigo));
    }

    [TestCase("fechaDesde=no-es-fecha")]
    [TestCase("clienteId=1%20OR%201=1")]
    public async Task EstacionalidadRechazaTiposMalformados(string consulta) =>
        Assert.That((await Cliente.GetAsync(RutaEstacionalidad + "?" + consulta)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));

    [Test]
    public void EstacionalidadExigePermisoDeConsultaDeReportes() => Assert.That(
        typeof(EstacionalidadController).GetMethod("Obtener")!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Policy, Is.EqualTo(Politicas.ReporteConsultar));
}
