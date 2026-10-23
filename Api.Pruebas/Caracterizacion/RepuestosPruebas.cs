using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class RepuestosPruebas : PruebaBase
{
    [Test]
    public async Task CrearRepuestoConCodigoRepetidoResponde409ConCodigoEstable()
    {
        var codigo = "REP-" + DatosDePrueba.Sufijo();
        var primero = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo,
            nombre = "Repuesto original",
            cantidad = 0,
            cantidadMinima = 0,
        });
        primero.EnsureSuccessStatusCode();

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo,
            nombre = "Otro repuesto",
            cantidad = 0,
            cantidadMinima = 0,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("CODIGO_REPUESTO_DUPLICADO"));
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("Ya existe un repuesto con ese código."));
    }
}
