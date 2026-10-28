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
using SOTRA_BACKEND.Operaciones;

namespace Api.Pruebas.Caracterizacion;

public class DemorasEtapasPruebas : PruebaBase
{
    private const string Ruta = "/api/v1/reportes/demoras-etapas";

    [TearDown]
    public async Task RestablecerReferenciasAsync()
    {
        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        await alcance.ServiceProvider.GetRequiredService<SotraContext>().Database.ExecuteSqlRawAsync("UPDATE operativo.umbral_etapa_ot SET umbral_horas = NULL");
    }

    private async Task<JsonElement> LeerAsync(string consulta = "")
    {
        var respuesta = await Cliente.GetAsync(Ruta + consulta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), await respuesta.Content.ReadAsStringAsync());
        return await respuesta.LeerJsonAsync();
    }

    private static JsonElement Etapa(JsonElement datos, string etapa) => datos.GetProperty("etapas").EnumerateArray().Single(e => e.GetProperty("etapa").GetString() == etapa);

    private async Task<long> CrearHistoriaAsync(JsonElement activo, params (string Estado, double Horas)[] pasos)
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/ordenes-trabajo", new
        {
            clienteId = activo.GetProperty("clienteId").GetInt64(),
            activoId = activo.GetProperty("id").GetInt64(),
            tipo = "correctivo",
            prioridad = "media",
            descripcion = "Historial conocido para A18",
            fechaPrevistaEjecucion = new DateOnly(2026, 10, 1),
            repuestos = Array.Empty<object>(),
        });
        respuesta.EnsureSuccessStatusCode();
        var id = (await respuesta.LeerJsonAsync()).GetProperty("id").GetInt64();
        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        var creada = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var fecha = creada;
        await contexto.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM operativo.historial_estado_ot WHERE id_orden = {id}");
        foreach (var (estado, horas) in pasos)
        {
            var estadoId = await contexto.EstadoOts.Where(e => e.Nombre == estado).Select(e => e.IdEstadoOt).SingleAsync();
            await contexto.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO operativo.historial_estado_ot (id_orden, id_estado_ot, fecha_hora) VALUES ({id}, {estadoId}, {fecha})");
            fecha = fecha.AddHours(horas);
        }
        DateTime? cierre = pasos[^1].Estado == "cerrado" ? fecha : null;
        await contexto.Database.ExecuteSqlInterpolatedAsync($"UPDATE operativo.orden_trabajo SET fecha_creada = {creada}, fecha_cierre = {cierre} WHERE id_orden = {id}");
        return id;
    }

    [Test]
    public async Task CalculaMediaMedianaYReferenciaHistoricaConUnoDosYTreintaHoras()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var previo = Etapa(await LeerAsync(), "pendiente");
        var muestraPrevia = previo.GetProperty("muestraHistorica").GetInt32();
        var mediaPrevia = previo.GetProperty("promedioHistoricoHoras").ValueKind == JsonValueKind.Null ? 0 : previo.GetProperty("promedioHistoricoHoras").GetDouble();
        foreach (var horas in new[] { 1, 2, 30 })
            await CrearHistoriaAsync(activo, ("pendiente", horas), ("en_progreso", 1), ("en_revision", 1), ("cerrado", 0));
        var datos = await LeerAsync($"?activoId={activo.GetProperty("id").GetInt64()}&fechaDesde=2026-10-01&fechaHasta=2026-10-01");
        var pendiente = Etapa(datos, "pendiente");
        Assert.Multiple(() =>
        {
            Assert.That(pendiente.GetProperty("promedioHoras").GetDouble(), Is.EqualTo(11));
            Assert.That(pendiente.GetProperty("medianaHoras").GetDouble(), Is.EqualTo(2));
            Assert.That(pendiente.GetProperty("percentil90Horas").GetDouble(), Is.EqualTo(24.4).Within(0.0001));
            Assert.That(pendiente.GetProperty("muestraHistorica").GetInt32(), Is.EqualTo(muestraPrevia + 3));
            Assert.That(pendiente.GetProperty("umbralHoras").GetDouble(), Is.EqualTo((mediaPrevia * muestraPrevia + 33) / (muestraPrevia + 3)).Within(0.00001));
            Assert.That(pendiente.GetProperty("origenUmbral").GetString(), Is.EqualTo("historico"));
        });
    }

    [Test]
    public async Task SeparaLaPausaSumaRetornosYListaExcesosConPaginacion()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var id = await CrearHistoriaAsync(activo, ("pendiente", 1), ("en_progreso", 2), ("pausado", 4),
            ("en_progreso", 3), ("en_revision", 1), ("pendiente", 1), ("en_progreso", 1), ("en_revision", 2), ("cerrado", 0));
        foreach (var etapa in new[] { "pendiente", "en_progreso", "revision", "pausa" })
            (await Cliente.PutAsJsonAsync(Ruta + "/umbrales/" + etapa, new { umbralHoras = 1 })).EnsureSuccessStatusCode();
        var consulta = $"?activoId={activo.GetProperty("id").GetInt64()}&tamanoPagina=2";
        var datos = await LeerAsync(consulta);
        Assert.That(Etapa(datos, "pendiente").GetProperty("promedioHoras").GetDouble(), Is.EqualTo(2));
        Assert.That(Etapa(datos, "en_progreso").GetProperty("promedioHoras").GetDouble(), Is.EqualTo(6));
        Assert.That(Etapa(datos, "pausa").GetProperty("promedioHoras").GetDouble(), Is.EqualTo(4));
        Assert.That(Etapa(datos, "revision").GetProperty("promedioHoras").GetDouble(), Is.EqualTo(3));
        var demoradas = datos.GetProperty("demoradas");
        Assert.That(demoradas.GetProperty("totalElementos").GetInt32(), Is.EqualTo(4));
        Assert.That(demoradas.GetProperty("elementos").GetArrayLength(), Is.EqualTo(2));
        var primera = demoradas.GetProperty("elementos")[0];
        Assert.That(primera.GetProperty("ordenId").GetInt64(), Is.EqualTo(id));
        Assert.That(primera.GetProperty("excesoHoras").GetDouble(), Is.EqualTo(5));
        Assert.That(primera.GetProperty("enCurso").GetBoolean(), Is.False);
        var segunda = await LeerAsync(consulta + "&pagina=2");
        Assert.That(segunda.GetProperty("demoradas").GetProperty("elementos").GetArrayLength(), Is.EqualTo(2));
        var filtrado = await LeerAsync(consulta + "&etapa=pausa");
        Assert.That(filtrado.GetProperty("demoradas").GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
        Assert.That(datos.GetRawText(), Does.Not.Contain("tecnico").And.Not.Contain("usuario").And.Not.Contain("responsable"));
    }

    [Test]
    public async Task ElUmbralAdmiteCeroValoresGrandesYRestablecerConNull()
    {
        foreach (double? valor in new double?[] { 0, 1e50, null })
        {
            var respuesta = await Cliente.PutAsJsonAsync(Ruta + "/umbrales/pendiente", new { umbralHoras = valor });
            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            var registro = await respuesta.LeerJsonAsync();
            Assert.That(registro.GetProperty("umbralHoras").ValueKind == JsonValueKind.Null ? null : (double?)registro.GetProperty("umbralHoras").GetDouble(), Is.EqualTo(valor));
        }
        Assert.That((await Cliente.PutAsJsonAsync(Ruta + "/umbrales/pendiente", new { umbralHoras = -1 })).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await Cliente.PutAsJsonAsync(Ruta + "/umbrales/pendiente", new { })).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await Cliente.PutAsJsonAsync(Ruta + "/umbrales/desconocida", new { umbralHoras = 3 })).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CuentaLaEtapaAbiertaHastaLaConsultaSinIncluirlaEnLaReferencia()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var previo = Etapa(await LeerAsync(), "pendiente").GetProperty("muestraHistorica").GetInt32();
        await CrearHistoriaAsync(activo, ("pendiente", 0));
        (await Cliente.PutAsJsonAsync(Ruta + "/umbrales/pendiente", new { umbralHoras = 0 })).EnsureSuccessStatusCode();
        var datos = await LeerAsync($"?activoId={activo.GetProperty("id").GetInt64()}");
        Assert.That(Etapa(datos, "pendiente").GetProperty("muestraHistorica").GetInt32(), Is.EqualTo(previo));
        Assert.That(Etapa(datos, "pendiente").GetProperty("promedioHoras").GetDouble(), Is.GreaterThan(1));
        Assert.That(datos.GetProperty("demoradas").GetProperty("elementos")[0].GetProperty("enCurso").GetBoolean(), Is.True);
        Assert.That(Etapa(datos, "pausa").GetProperty("promedioHoras").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [TestCase("?pagina=0")]
    [TestCase("?tamanoPagina=101")]
    [TestCase("?etapa=%27%20OR%201%3D1--")]
    [TestCase("?fechaDesde=2026-10-02&fechaHasta=2026-10-01")]
    public async Task ValidaFiltrosSinExponerErroresTecnicos(string consulta)
    {
        var respuesta = await Cliente.GetAsync(Ruta + consulta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await respuesta.LeerJsonAsync()).TryGetProperty("codigo", out _), Is.True);
    }

    [Test]
    public void SoloElAdministradorConfiguraYAdministradorOJefeConsultan()
    {
        var tipo = typeof(DemorasEtapasController);
        Assert.That(tipo.GetMethod("Obtener")!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Policy, Is.EqualTo(Politicas.ReporteConsultar));
        foreach (var metodo in new[] { "ObtenerUmbrales", "GuardarUmbral" })
            Assert.That(tipo.GetMethod(metodo)!.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Policy, Is.EqualTo(Politicas.AvisoConfigurar));
    }

    [Test]
    public async Task SinHistoriaNiUmbralNoInventaUnaReferencia()
    {
        EntornoDePruebas.VerificarBaseDePruebas();
        using var alcance = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<SotraContext>();
        await using var transaccion = await contexto.Database.BeginTransactionAsync();
        await contexto.Database.ExecuteSqlRawAsync("DELETE FROM operativo.historial_estado_ot");
        await contexto.Database.ExecuteSqlRawAsync("UPDATE operativo.umbral_etapa_ot SET umbral_horas = NULL");
        var resultado = await new DemorasEtapasDAO(contexto).ObtenerAsync(new FiltroAnalitica(null, null, null, null), null, 1, 20, DateTime.UtcNow, CancellationToken.None);
        using var documento = JsonDocument.Parse(resultado);
        foreach (var etapa in documento.RootElement.GetProperty("Etapas").EnumerateArray())
        {
            Assert.That(etapa.GetProperty("OrigenUmbral").GetString(), Is.EqualTo("sin_datos"));
            Assert.That(etapa.GetProperty("UmbralHoras").ValueKind, Is.EqualTo(JsonValueKind.Null));
        }
        await transaccion.RollbackAsync();
    }

    [TestCase(1, 1, 0)]
    [TestCase(0, 0, 0)]
    [TestCase(1.01, 1, 1)]
    public async Task SoloEsDemoraSiSuperaEstrictamenteElUmbral(double horas, double umbral, int esperadas)
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        await CrearHistoriaAsync(activo, ("pendiente", horas), ("en_progreso", 1), ("en_revision", 1), ("cerrado", 0));
        (await Cliente.PutAsJsonAsync(Ruta + "/umbrales/pendiente", new { umbralHoras = umbral })).EnsureSuccessStatusCode();
        var datos = await LeerAsync($"?activoId={activo.GetProperty("id").GetInt64()}&etapa=pendiente");
        Assert.That(datos.GetProperty("demoradas").GetProperty("totalElementos").GetInt32(), Is.EqualTo(esperadas));
    }
}
