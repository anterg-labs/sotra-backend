using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SOTRA_BACKEND.Contexto;

namespace Api.Pruebas.Caracterizacion;

public partial class AnaliticaAvanzadaPruebas
{
    private async Task<long> CrearOrdenConHorasAsync(JsonElement activo, string tipo, DateTime creada, double horas, bool conPausa = false)
    {
        var id = await CrearOrdenAsync(activo, tipo);
        if (conPausa)
        {
            await AsignarTecnicoAsync(id);
            foreach (var estado in new[] { "en_progreso", "pausado", "en_progreso", "en_revision", "cerrado" })
            {
                (await Cliente.PatchAsJsonAsync($"/api/v1/ordenes-trabajo/{id}/estado",
                    new { estado, motivo = "Pausa para comprobar las horas transcurridas" })).EnsureSuccessStatusCode();
            }
        }
        else
        {
            await CerrarAsync(id);
        }

        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        var cierre = creada.AddHours(horas);
        await contexto.Database.ExecuteSqlInterpolatedAsync($"UPDATE operativo.orden_trabajo SET fecha_creada = {creada}, fecha_inicio = {creada}, fecha_cierre = {cierre} WHERE id_orden = {id}");
        if (conPausa)
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                WITH pasos AS (
                    SELECT id_h_estado_ot, row_number() OVER (ORDER BY fecha_hora, id_h_estado_ot) - 1 AS paso
                    FROM operativo.historial_estado_ot WHERE id_orden = {id}
                )
                UPDATE operativo.historial_estado_ot AS h
                SET fecha_hora = {creada} + make_interval(secs => pasos.paso * {horas * 3600.0 / 5})
                FROM pasos WHERE h.id_h_estado_ot = pasos.id_h_estado_ot
                """);
        }
        if (conPausa)
        {
            await contexto.Database.ExecuteSqlInterpolatedAsync($"""
                WITH pasos AS (
                    SELECT id_h_estado_ot, row_number() OVER (ORDER BY fecha_hora, id_h_estado_ot) - 1 AS paso
                    FROM operativo.historial_estado_ot WHERE id_orden = {id}
                )
                UPDATE operativo.historial_estado_ot AS h
                SET fecha_hora = {creada} + make_interval(secs => pasos.paso * {horas * 3600.0 / 5})
                FROM pasos WHERE h.id_h_estado_ot = pasos.id_h_estado_ot
                """);
        }
        return id;
    }

    [Test]
    public async Task LaPlanificacionEnHorasIncluyePausasYComparaPeriodosSinContarAbiertas()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var fecha = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        await CrearOrdenConHorasAsync(activo, "preventivo", fecha, 10, conPausa: true);
        await CrearOrdenConHorasAsync(activo, "correctivo", fecha.AddDays(30), 30);
        await CrearOrdenAsync(activo, "preventivo");
        await CrearOrdenConHorasAsync(activo, "preventivo", fecha.AddDays(-31), 10);
        await CrearOrdenConHorasAsync(activo, "correctivo", fecha.AddDays(-31), 10);
        await CrearOrdenConHorasAsync(activo, "preventivo", fecha.AddYears(-1), 30);
        await CrearOrdenConHorasAsync(activo, "correctivo", fecha.AddYears(-1), 10);
        var ajeno = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenConHorasAsync(ajeno, "preventivo", fecha, 1000);

        var consulta = $"clienteId={clienteId}&fechaDesde=2026-10-01&fechaHasta=2026-10-31";
        var desempeno = await LeerAsync("/api/v1/reportes/desempeno?" + consulta);
        var datos = desempeno.GetProperty("planificacion");
        var resumen = datos.GetProperty("resumen");
        Assert.Multiple(() =>
        {
            Assert.That(resumen.GetProperty("horasPreventivas").GetDouble(), Is.EqualTo(10));
            Assert.That(resumen.GetProperty("horasCorrectivas").GetDouble(), Is.EqualTo(30));
            Assert.That(resumen.GetProperty("horasTotales").GetDouble(), Is.EqualTo(40));
            Assert.That(resumen.GetProperty("porcentajePlanificado").GetDouble(), Is.EqualTo(25));
            Assert.That(resumen.GetProperty("ordenesCerradas").GetInt32(), Is.EqualTo(2));
            Assert.That(desempeno.GetProperty("flujo").GetProperty("pausas").GetProperty("porMotivo")[0].GetProperty("horas").GetDouble(), Is.EqualTo(2));
            Assert.That(desempeno.GetProperty("flujo").GetProperty("pausas").GetProperty("porMotivo")[0].GetProperty("horas").GetDouble(), Is.EqualTo(2));
            Assert.That(datos.GetProperty("porMes")[0].GetProperty("resumen").GetRawText(), Is.EqualTo(resumen.GetRawText()));
            Assert.That(datos.GetProperty("porTipo")[0].GetProperty("resumen").GetRawText(), Is.EqualTo(resumen.GetRawText()));
            Assert.That(datos.GetRawText(), Does.Not.Contain("tecnico").And.Not.Contain("usuario").And.Not.Contain("responsable"));
        });
        var comparativa = await LeerAsync("/api/v1/reportes/analisis/comparativa?" + consulta);
        var metrica = Metrica(comparativa, "planificado_horas_porcentaje");
        Assert.Multiple(() =>
        {
            Assert.That(metrica.GetProperty("actual").GetDouble(), Is.EqualTo(25));
            Assert.That(metrica.GetProperty("anterior").GetDouble(), Is.EqualTo(50));
            Assert.That(metrica.GetProperty("anioAnterior").GetDouble(), Is.EqualTo(75));
            Assert.That(metrica.GetProperty("variacionPorcentual").GetDouble(), Is.EqualTo(-50));
            Assert.That(metrica.GetProperty("muestra").GetInt32(), Is.EqualTo(2));
            Assert.That(metrica.GetProperty("serie")[5].GetProperty("valor").GetDouble(), Is.EqualTo(25));
        });
    }

    [TestCase("preventivo", 8, 100)]
    [TestCase("correctivo", 8, 0)]
    [TestCase("preventivo", 0, null)]
    public async Task LaPlanificacionDistingueCeroDeCienYSinHoras(string tipo, double horas, double? esperado)
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenConHorasAsync(activo, tipo, new DateTime(2026, 10, 15, 0, 0, 0, DateTimeKind.Utc), horas);
        var consulta = $"activoId={activo.GetProperty("id").GetInt64()}&fechaDesde=2026-10-01&fechaHasta=2026-10-31";
        var datos = (await LeerAsync("/api/v1/reportes/desempeno?" + consulta)).GetProperty("planificacion");
        var porcentaje = datos.GetProperty("resumen").GetProperty("porcentajePlanificado");
        var valor = Metrica(await LeerAsync("/api/v1/reportes/analisis/comparativa?" + consulta), "planificado_horas_porcentaje").GetProperty("actual");
        Assert.That(porcentaje.ValueKind == JsonValueKind.Null ? null : (double?)porcentaje.GetDouble(), Is.EqualTo(esperado));
        Assert.That(valor.ValueKind == JsonValueKind.Null ? null : (double?)valor.GetDouble(), Is.EqualTo(esperado));
    }

    [Test]
    public async Task LaPlanificacionSinCierresDevuelveResumenVacioYConservaElPeriodo()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearOrdenAsync(activo, "preventivo");
        var consulta = $"activoId={activo.GetProperty("id").GetInt64()}";
        var datos = (await LeerAsync("/api/v1/reportes/desempeno?" + consulta)).GetProperty("planificacion");
        Assert.That(datos.GetProperty("resumen").GetProperty("porcentajePlanificado").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(datos.GetProperty("porMes").GetArrayLength(), Is.Zero);
        Assert.That(datos.GetProperty("porTipo").GetArrayLength(), Is.Zero);
        Assert.That(datos.GetProperty("periodo").GetProperty("hasta").GetString(), Is.EqualTo(Hoy));
    }

    [Test]
    public async Task LaPlanificacionSeparaMesesYTiposYFiltraElActivo()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var clienteId = activo.GetProperty("clienteId").GetInt64();
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var modelo = await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipo.GetProperty("id").GetInt64());
        var segundo = await (await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(clienteId, modelo.GetProperty("id").GetInt64()))).LeerJsonAsync();
        await CrearOrdenConHorasAsync(activo, "preventivo", new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), 10);
        await CrearOrdenConHorasAsync(segundo, "correctivo", new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc), 30);
        var consulta = $"clienteId={clienteId}&fechaDesde=2026-09-01&fechaHasta=2026-10-31";
        var datos = (await LeerAsync("/api/v1/reportes/desempeno?" + consulta)).GetProperty("planificacion");
        Assert.That(datos.GetProperty("porMes").GetArrayLength(), Is.EqualTo(2));
        Assert.That(datos.GetProperty("porTipo").GetArrayLength(), Is.EqualTo(2));
        Assert.That(datos.GetProperty("porTipo").EnumerateArray().Select(fila => fila.GetProperty("resumen").GetProperty("porcentajePlanificado").GetDouble()), Is.EquivalentTo(new[] { 0.0, 100.0 }));
        var filtrado = (await LeerAsync("/api/v1/reportes/desempeno?" + consulta + $"&activoId={segundo.GetProperty("id").GetInt64()}")).GetProperty("planificacion");
        Assert.That(filtrado.GetProperty("resumen").GetProperty("horasPreventivas").GetDouble(), Is.Zero);
        Assert.That(filtrado.GetProperty("resumen").GetProperty("horasCorrectivas").GetDouble(), Is.EqualTo(30));
    }
}
