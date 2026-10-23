using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using NUnit.Framework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SOTRA_BACKEND.Contexto;

namespace Api.Pruebas.Caracterizacion;

public class ContratosDeActualizacionPruebas : PruebaBase
{
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t")]
    public async Task BaseDeDatosTambienRechazaUnObligatorioEnBlancoSinPerderElValor(string valor)
    {
        var original = await DatosDePrueba.CrearClienteAsync(Cliente);
        var id = original.GetProperty("id").GetInt64();
        using var ambito = EntornoDePruebas.Fabrica.Services.CreateScope();
        var contexto = ambito.ServiceProvider.GetRequiredService<SotraContext>();

        var error = await Assert.ThrowsAsync<PostgresException>(async () =>
            await contexto.Database.ExecuteSqlInterpolatedAsync($"UPDATE operativo.cliente SET razon_social = {valor} WHERE id_cliente = {id}"));

        Assert.That(error!.SqlState, Is.EqualTo("23514"));
        var guardado = await (await Cliente.GetAsync($"/api/v1/clientes/{id}")).LeerJsonAsync();
        Assert.That(guardado.GetProperty("razonSocial").GetString(), Is.EqualTo(original.GetProperty("razonSocial").GetString()));
    }

    [TestCase("usuarios")]
    [TestCase("clientes")]
    [TestCase("activos")]
    [TestCase("tipos-maquina")]
    [TestCase("modelos-maquina")]
    [TestCase("repuestos")]
    [TestCase("documentos-tecnicos")]
    [TestCase("ordenes-trabajo")]
    public async Task PutConUnSoloCampoNoSeInterpretaComoActualizacionParcial(string recurso)
    {
        var respuesta = await Cliente.PutAsJsonAsync($"/api/v1/{recurso}/999999999", new { nombre = "Solo un campo" });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [TestCase("null")]
    [TestCase("\"\"")]
    [TestCase("\"   \"")]
    public async Task PutCompletoBorraOpcionalesDeClienteExplicitamenteVaciados(string valor)
    {
        var original = await DatosDePrueba.CrearClienteAsync(Cliente);
        var id = original.GetProperty("id").GetInt64();
        var cuerpo = JsonNode.Parse(original.GetRawText())!.AsObject();
        foreach (var campo in new[] { "correo", "telefono", "direccion", "observaciones" })
            cuerpo[campo] = JsonNode.Parse(valor);

        var respuesta = await Cliente.PutAsJsonAsync($"/api/v1/clientes/{id}", cuerpo);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var guardado = await (await Cliente.GetAsync($"/api/v1/clientes/{id}")).LeerJsonAsync();
        foreach (var campo in new[] { "correo", "telefono", "direccion", "observaciones" })
            Assert.That(guardado.GetProperty(campo).ValueKind, Is.EqualTo(JsonValueKind.Null), campo);
        Assert.That(guardado.GetProperty("razonSocial").GetString(), Is.EqualTo(original.GetProperty("razonSocial").GetString()));
    }

    [TestCase("omitido")]
    [TestCase("obligatorioNull")]
    [TestCase("obligatorioBlanco")]
    public async Task PutInvalidoDeClienteNoBorraNiModificaDatos(string variante)
    {
        var original = await DatosDePrueba.CrearClienteAsync(Cliente);
        var id = original.GetProperty("id").GetInt64();
        var cuerpo = JsonNode.Parse(original.GetRawText())!.AsObject();
        if (variante == "omitido") cuerpo.Remove("direccion");
        else cuerpo["razonSocial"] = variante == "obligatorioNull" ? null : JsonValue.Create("   ");

        var respuesta = await Cliente.PutAsJsonAsync($"/api/v1/clientes/{id}", cuerpo);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var guardado = await (await Cliente.GetAsync($"/api/v1/clientes/{id}")).LeerJsonAsync();
        Assert.That(guardado.GetRawText(), Is.EqualTo(original.GetRawText()));
    }

    [Test]
    public async Task PutDeActivoPermiteBorrarTextosYAnoOpcionales()
    {
        var original = await DatosDePrueba.CrearActivoAsync(Cliente);
        var id = original.GetProperty("id").GetInt64();
        var cuerpo = JsonNode.Parse(original.GetRawText())!.AsObject();
        cuerpo["nombre"] = "";
        cuerpo["ubicacion"] = "   ";
        cuerpo["descripcion"] = null;
        cuerpo["anioFabricacion"] = null;

        var respuesta = await Cliente.PutAsJsonAsync($"/api/v1/activos/{id}", cuerpo);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var guardado = await (await Cliente.GetAsync($"/api/v1/activos/{id}")).LeerJsonAsync();
        foreach (var campo in new[] { "nombre", "ubicacion", "descripcion", "anioFabricacion" })
            Assert.That(guardado.GetProperty(campo).ValueKind, Is.EqualTo(JsonValueKind.Null), campo);
    }

    [Test]
    public async Task PatchDeBajaDeUsuarioSoloCambiaElEstado()
    {
        var original = await DatosDePrueba.CrearUsuarioAsync(Cliente, DatosDePrueba.UsuarioNuevo(idsRol: [3]));
        var id = original.GetProperty("id").GetInt64();
        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/usuarios/{id}", new { estado = "inactivo" });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var guardado = await (await Cliente.GetAsync($"/api/v1/usuarios/{id}")).LeerJsonAsync();
        foreach (var campo in new[] { "nombre", "apellido", "correo", "telefono", "roles", "fechaIngreso" })
            Assert.That(guardado.GetProperty(campo).GetRawText(), Is.EqualTo(original.GetProperty(campo).GetRawText()), campo);
        Assert.That(guardado.GetProperty("estado").GetString(), Is.EqualTo("inactivo"));
    }

    [Test]
    public async Task PatchDeStockMinimoSinValorNoLoPoneEnCero()
    {
        var creado = await (await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        { codigo = "NUL-" + DatosDePrueba.Sufijo(), nombre = "Filtro", cantidad = 10, cantidadMinima = 4 })).LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt64();
        var respuesta = await Cliente.PatchAsJsonAsync($"/api/v1/repuestos/{id}/stock-minimo", new { });
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var guardado = await (await Cliente.GetAsync($"/api/v1/repuestos/{id}")).LeerJsonAsync();
        Assert.That(guardado.GetProperty("cantidadMinima").GetInt32(), Is.EqualTo(4));
    }
}
