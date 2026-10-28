using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Controladores;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public sealed class VisibilidadReportesPruebas : PruebaBase
{
    private const string Ruta = "/api/v1/reportes/visibilidad";

    [Test]
    public async Task PermiteOcultarUnElementoSinModificarLosDemasYPersiste()
    {
        var codigo = "grafico_prueba_" + Guid.NewGuid().ToString("N");
        var respuesta = await Cliente.PutAsJsonAsync(Ruta + "/" + codigo, new { visible = false });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var listado = await (await Cliente.GetAsync(Ruta)).LeerJsonAsync();
        Assert.That(listado.EnumerateArray().Single(item => item.GetProperty("codigo").GetString() == codigo)
            .GetProperty("visible").GetBoolean(), Is.False);
        (await Cliente.PutAsJsonAsync(Ruta + "/" + codigo, new { visible = true })).EnsureSuccessStatusCode();
        listado = await (await Cliente.GetAsync(Ruta)).LeerJsonAsync();
        Assert.That(listado.EnumerateArray().Single(item => item.GetProperty("codigo").GetString() == codigo)
            .GetProperty("visible").GetBoolean(), Is.True);
    }

    [TestCase("MAL_CODIGO")]
    [TestCase("codigo%20invalido")]
    public async Task RechazaCodigosInvalidos(string codigo)
    {
        var respuesta = await Cliente.PutAsJsonAsync(Ruta + "/" + codigo, new { visible = false });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public void LecturaYEscrituraExigenPermisosDistintos()
    {
        var metodos = typeof(VisibilidadReportesController).GetMethods();
        Assert.That(metodos.Single(m => m.Name == "Listar").GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Policy, Is.EqualTo(Politicas.ReporteConsultar));
        Assert.That(metodos.Single(m => m.Name == "Actualizar").GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .Cast<AuthorizeAttribute>().Single().Policy, Is.EqualTo(Politicas.AvisoConfigurar));
    }
}
