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
            nombre = "Repuesto original " + codigo,
            cantidad = 0,
            cantidadMinima = 0,
        });
        primero.EnsureSuccessStatusCode();

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo,
            nombre = "Otro repuesto " + codigo,
            cantidad = 0,
            cantidadMinima = 0,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("CODIGO_REPUESTO_DUPLICADO"));
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("Ya existe un repuesto con ese código."));
    }

    [Test]
    public async Task CrearRepuestoGuardaCategoriaCompatibilidadUnidadYObservaciones()
    {
        var sufijo = DatosDePrueba.Sufijo();
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "REP-" + sufijo,
            nombre = "Aceite " + sufijo,
            categoria = "Lubricantes",
            modeloCompatibilidad = "Serie 300",
            unidadMedida = "litro",
            observaciones = "Guardar lejos del calor.",
            cantidad = 5,
            cantidadMinima = 1,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("categoria").GetString(), Is.EqualTo("Lubricantes"));
        Assert.That(cuerpo.GetProperty("modeloCompatibilidad").GetString(), Is.EqualTo("Serie 300"));
        Assert.That(cuerpo.GetProperty("unidadMedida").GetString(), Is.EqualTo("litro"));
        Assert.That(cuerpo.GetProperty("observaciones").GetString(), Is.EqualTo("Guardar lejos del calor."));
    }

    [Test]
    public async Task SinUnidadDeMedidaElRepuestoSeGuardaEnUnidades()
    {
        var sufijo = DatosDePrueba.Sufijo();
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "REP-" + sufijo,
            nombre = "Tornillo " + sufijo,
            cantidad = 0,
            cantidadMinima = 0,
        });

        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("unidadMedida").GetString(), Is.EqualTo("unidad"));
        Assert.That(cuerpo.GetProperty("categoria").GetString(), Is.Empty);
    }

    [Test]
    public async Task CrearRepuestoConNombreRepetidoSinImportarMayusculasResponde409ConCodigoEstable()
    {
        var sufijo = DatosDePrueba.Sufijo();
        var primero = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "REP-A" + sufijo,
            nombre = "Correa " + sufijo,
            cantidad = 0,
            cantidadMinima = 0,
        });
        primero.EnsureSuccessStatusCode();

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "REP-B" + sufijo,
            nombre = "  CORREA " + sufijo + " ",
            cantidad = 0,
            cantidadMinima = 0,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("NOMBRE_REPUESTO_DUPLICADO"));
    }

    [Test]
    public async Task ElListadoFiltraPorCategoriaSinDistinguirMayusculas()
    {
        var sufijo = DatosDePrueba.Sufijo();
        var categoria = "Categoria" + sufijo;
        foreach (var (nombre, cat) in new[] { ("Uno ", categoria), ("Dos ", "Otra" + sufijo) })
        {
            (await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
            {
                codigo = "REP-" + nombre.Trim() + sufijo,
                nombre = nombre + sufijo,
                categoria = cat,
                cantidad = 0,
                cantidadMinima = 0,
            })).EnsureSuccessStatusCode();
        }

        var listado = await (await Cliente.GetAsync("/api/v1/repuestos?categoria=" + categoria.ToUpperInvariant())).LeerJsonAsync();

        var elementos = listado.GetProperty("elementos").EnumerateArray().ToList();
        Assert.That(elementos, Has.Count.EqualTo(1));
        Assert.That(elementos[0].GetProperty("categoria").GetString(), Is.EqualTo(categoria));
    }

    [Test]
    public async Task ModificarRepuestoCambiaYVaciaLosDatosNuevos()
    {
        var sufijo = DatosDePrueba.Sufijo();
        var creado = await (await Cliente.PostAsJsonAsync("/api/v1/repuestos", new
        {
            codigo = "REP-" + sufijo,
            nombre = "Filtro " + sufijo,
            categoria = "Filtros",
            observaciones = "Original",
            cantidad = 0,
            cantidadMinima = 0,
        })).LeerJsonAsync();
        var id = creado.GetProperty("id").GetInt64();

        var respuesta = await Cliente.PutAsJsonAsync($"/api/v1/repuestos/{id}", new
        {
            codigo = "REP-" + sufijo,
            nombre = "Filtro " + sufijo,
            descripcion = (string?)null,
            marca = (string?)null,
            ubicacion = (string?)null,
            categoria = (string?)null,
            modeloCompatibilidad = "Modelo X",
            unidadMedida = (string?)null,
            observaciones = "   ",
            cantidadMinima = 0,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var guardado = await (await Cliente.GetAsync($"/api/v1/repuestos/{id}")).LeerJsonAsync();
        Assert.That(guardado.GetProperty("categoria").GetString(), Is.Empty);
        Assert.That(guardado.GetProperty("modeloCompatibilidad").GetString(), Is.EqualTo("Modelo X"));
        Assert.That(guardado.GetProperty("unidadMedida").GetString(), Is.EqualTo("unidad"));
        Assert.That(guardado.GetProperty("observaciones").GetString(), Is.Empty);
    }
}
