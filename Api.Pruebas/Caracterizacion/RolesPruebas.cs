using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class RolesPruebas : PruebaBase
{
    [Test]
    public async Task ListarRolesDevuelveLosCuatroRolesInicialesOrdenadosPorId()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/roles");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var roles = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        Assert.That(roles.ValueKind, Is.EqualTo(JsonValueKind.Array));

        var nombres = roles.EnumerateArray().Select(rol => rol.GetProperty("nombre").GetString()).ToList();
        Assert.That(nombres, Is.EqualTo(new[] { "Administrador", "Jefe de mantenimiento", "Técnico", "Pañolero" }));
    }

    [Test]
    public async Task ListarRolesExponeIdNombreYEstado()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/roles");

        var roles = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var propiedades = roles[0].EnumerateObject().Select(propiedad => propiedad.Name).ToList();
        Assert.That(propiedades, Is.EquivalentTo(new[] { "id", "nombre", "estado" }));
    }

    [Test]
    public async Task ListarRolesInformaElEstadoComoTexto()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/roles");

        var roles = await respuesta.Content.ReadFromJsonAsync<JsonElement>();
        var estados = roles.EnumerateArray().Select(rol => rol.GetProperty("estado").GetString()).ToList();
        Assert.That(estados, Has.All.EqualTo("activo"));
    }
}
