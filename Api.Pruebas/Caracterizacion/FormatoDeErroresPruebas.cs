using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

/// <summary>Todo error explícito de un controlador usa ProblemDetails con <c>codigo</c> y <c>mensaje</c> (Criterios 7.2).</summary>
public class FormatoDeErroresPruebas : PruebaBase
{
    [TestCase("/api/v1/reportes/tendencias?agrupacion=anio", HttpStatusCode.BadRequest, "AGRUPACION_INVALIDA")]
    [TestCase("/api/v1/reportes/indicadores/csv?fechaDesde=2026-02-01&fechaHasta=2026-01-01", HttpStatusCode.BadRequest, "RANGO_FECHAS_INVALIDO")]
    [TestCase("/api/v1/reportes/inexistente/csv", HttpStatusCode.BadRequest, "TIPO_REPORTE_INVALIDO")]
    [TestCase("/api/v1/repuestos?estadoStock=otro", HttpStatusCode.BadRequest, "ESTADO_STOCK_INVALIDO")]
    public async Task UnErrorExplicitoDevuelveProblemDetailsConCodigoYMensaje(string ruta, HttpStatusCode estado, string codigo)
    {
        var respuesta = await Cliente.GetAsync(ruta);

        Assert.That(respuesta.StatusCode, Is.EqualTo(estado));
        Assert.That(respuesta.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/problem+json"));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("status").GetInt32(), Is.EqualTo((int)estado));
        Assert.That(cuerpo.GetProperty("title").GetString(), Is.Not.Empty);
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo(codigo));
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.Not.Empty);
    }

    [TestCase("/api/v1/clientes/1")]
    [TestCase("/api/v1/usuarios/1")]

    [TestCase("/api/v1/activos/1")]
    [TestCase("/api/v1/tipos-maquina/1")]
    [TestCase("/api/v1/modelos-maquina/1")]
    [TestCase("/api/v1/repuestos/1")]
    public async Task UnEstadoInvalidoTieneCodigoEstable(string ruta)
    {
        var respuesta = await Cliente.PatchAsJsonAsync(ruta, new { estado = "otro" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.TryGetProperty("codigo", out var codigo), Is.True, "el error debe traer un código");
        Assert.That(codigo.GetString(), Is.Not.Empty);
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.Not.Empty);
    }
}
