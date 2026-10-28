using System.Net;
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
    private const string RutaEdadUso = "/api/v1/reportes/analisis/desgaste-edad-uso";

    [Test]
    public async Task EdadYUsoDistingueActivosDeLaMismaEdadYNoInventaLecturas()
    {
        var primero = await DatosDePrueba.CrearActivoAsync(Cliente);
        var segundo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var tercero = await DatosDePrueba.CrearActivoAsync(Cliente);
        var primeroId = primero.GetProperty("id").GetInt64();
        var segundoId = segundo.GetProperty("id").GetInt64();
        var terceroId = tercero.GetProperty("id").GetInt64();
        var primeraOrden = await CrearOrdenAsync(primero, "correctivo");
        var segundaOrden = await CrearOrdenAsync(primero, "correctivo");
        var terceraOrden = await CrearOrdenAsync(segundo, "correctivo");
        await CrearOrdenAsync(tercero, "preventivo");

        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        var anio = DateTime.UtcNow.Year - 5;
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE operativo.activo SET anio_fabricacion = {anio}
            WHERE id_activo IN ({primeroId}, {segundoId})
            """);
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE operativo.activo SET unidad_uso = 'kilometros'
            WHERE id_activo = {segundoId}
            """);
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE operativo.activo SET anio_fabricacion = NULL
            WHERE id_activo = {terceroId}
            """);
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE operativo.orden_trabajo SET lectura_uso = 100
            WHERE id_orden = {primeraOrden}
            """);
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE operativo.orden_trabajo SET lectura_uso = 200
            WHERE id_orden = {segundaOrden}
            """);
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE operativo.orden_trabajo SET lectura_uso = 1000
            WHERE id_orden = {terceraOrden}
            """);

        var datos = await LeerAsync($"{RutaEdadUso}?fechaDesde={Hoy}&fechaHasta={Hoy}");
        var activos = datos.GetProperty("activos").EnumerateArray().ToDictionary(fila => fila.GetProperty("activoId").GetInt64());
        Assert.Multiple(() =>
        {
            Assert.That(activos[primeroId].GetProperty("edadAnios").GetInt32(), Is.EqualTo(5));
            Assert.That(activos[segundoId].GetProperty("edadAnios").GetInt32(), Is.EqualTo(5));
            Assert.That(activos[primeroId].GetProperty("correctivas").GetInt32(), Is.EqualTo(2));
            Assert.That(activos[segundoId].GetProperty("correctivas").GetInt32(), Is.EqualTo(1));
            Assert.That(activos[segundoId].GetProperty("unidadUso").GetString(), Is.EqualTo("kilometros"));
            Assert.That(activos[segundoId].GetProperty("lecturaUso").GetDecimal(), Is.EqualTo(1000));
            Assert.That(activos[terceroId].GetProperty("edadAnios").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
            Assert.That(activos[terceroId].GetProperty("lecturaUso").ValueKind, Is.EqualTo(System.Text.Json.JsonValueKind.Null));
            Assert.That(activos[terceroId].GetProperty("correctivas").GetInt32(), Is.Zero);
            Assert.That(datos.GetProperty("conEdad").GetInt32(), Is.GreaterThanOrEqualTo(2));
        });
        Assert.That(datos.ToString(), Does.Not.Contain("tecnico"));
    }

    [Test]
    public async Task EdadYUsoConservaUltimaLecturaAnteriorAlPeriodoSinContarLaOrdenAntigua()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var antigua = await CrearOrdenAsync(activo, "correctivo");
        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE operativo.orden_trabajo
            SET fecha_creada = {DateTime.UtcNow.AddDays(-3)}, lectura_uso = 450
            WHERE id_orden = {antigua}
            """);
        await CrearOrdenAsync(activo, "correctivo");

        var datos = await LeerAsync($"{RutaEdadUso}?activoId={activo.GetProperty("id").GetInt64()}&fechaDesde={Hoy}&fechaHasta={Hoy}");
        Assert.That(datos.GetProperty("totalActivos").GetInt32(), Is.EqualTo(1));
        var fila = datos.GetProperty("activos")[0];
        Assert.That(fila.GetProperty("correctivas").GetInt32(), Is.EqualTo(1));
        Assert.That(fila.GetProperty("lecturaUso").GetDecimal(), Is.EqualTo(450));
        Assert.That(fila.GetProperty("fechaLectura").GetString(), Is.Not.Empty);
    }

    [TestCase("fechaDesde=2026-02-01&fechaHasta=2026-01-01", "RANGO_FECHAS_INVALIDO")]
    [TestCase("fechaHasta=9999-12-31", "RANGO_FECHAS_INVALIDO")]
    [TestCase("activoId=0", "FILTROS_INVALIDOS")]
    public async Task EdadYUsoRechazaFiltrosInvalidos(string consulta, string codigo)
    {
        var respuesta = await Cliente.GetAsync(RutaEdadUso + "?" + consulta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo(codigo));
    }

    [Test]
    public void EdadYUsoExigePermisoDeReportes() => Assert.That(
        typeof(DesgasteEdadUsoController).GetMethod("Obtener")!.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Policy, Is.EqualTo(Politicas.ReporteConsultar));
}
