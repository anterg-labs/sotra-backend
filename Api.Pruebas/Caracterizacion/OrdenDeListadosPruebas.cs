using System.Net;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

/// <summary>Los listados se ordenan en el servidor por una lista cerrada de columnas (<c>ordenarPor</c> y <c>direccionOrden</c>).</summary>
public class OrdenDeListadosPruebas : PruebaBase
{
    private async Task<List<string>> LeerColumnaAsync(string ruta, string propiedad)
    {
        var respuesta = await Cliente.GetAsync(ruta);
        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), ruta);
        var pagina = await respuesta.LeerJsonAsync();
        return pagina.GetProperty("elementos").EnumerateArray()
            .Select(e => e.GetProperty(propiedad).ToString())
            .ToList();
    }

    [TestCase("razonSocial")]
    [TestCase("nombreContacto")]
    [TestCase("correo")]
    [TestCase("telefono")]
    [TestCase("cantidadActivosAsociados")]
    [TestCase("estado")]
    [TestCase("creadoEn")]
    public async Task LosClientesAceptanCadaColumnaOrdenable(string columna)
    {
        var respuesta = await Cliente.GetAsync($"/api/v1/clientes?ordenarPor={columna}&direccionOrden=desc");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task LosClientesSeOrdenanPorRazonSocialEnAmbosSentidos()
    {
        var marca = DatosDePrueba.Sufijo();
        foreach (var inicial in new[] { "B", "A", "C" })
        {
            await DatosDePrueba.CrearClienteAsync(Cliente,
                DatosDePrueba.ClienteNuevo(razonSocial: $"{inicial}-orden {marca}"));
        }

        var ascendente = await LeerColumnaAsync($"/api/v1/clientes?buscar={marca}&ordenarPor=razonSocial&direccionOrden=asc", "razonSocial");
        var descendente = await LeerColumnaAsync($"/api/v1/clientes?buscar={marca}&ordenarPor=razonSocial&direccionOrden=desc", "razonSocial");

        Assert.That(ascendente, Is.EqualTo(new[] { $"A-orden {marca}", $"B-orden {marca}", $"C-orden {marca}" }));
        Assert.That(descendente, Is.EqualTo(ascendente.AsEnumerable().Reverse().ToList()));
    }

    [Test]
    public async Task UnaColumnaFueraDeLaListaSeRechazaConCodigoEstable()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/clientes?ordenarPor=contrasena");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("ORDEN_INVALIDO"));
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Does.Contain("razonSocial"));
    }

    [TestCase("razonSocial; DROP TABLE cliente")]
    [TestCase("1 OR 1=1")]
    public async Task UnTextoMaliciosoEnElOrdenSeRechazaSinLlegarALaBase(string columna)
    {
        var respuesta = await Cliente.GetAsync("/api/v1/clientes?ordenarPor=" + Uri.EscapeDataString(columna));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task LaPaginacionConOrdenNoRepiteNiPierdeRegistros()
    {
        var primera = await LeerColumnaAsync("/api/v1/clientes?ordenarPor=estado&pagina=1&tamanoPagina=5", "id");
        var segunda = await LeerColumnaAsync("/api/v1/clientes?ordenarPor=estado&pagina=2&tamanoPagina=5", "id");

        Assert.That(primera.Intersect(segunda), Is.Empty);
    }

    [TestCase("usuarios", "nombre")]
    [TestCase("usuarios", "correo")]
    [TestCase("usuarios", "roles")]
    [TestCase("usuarios", "estado")]
    [TestCase("usuarios", "creadoEn")]
    [TestCase("activos", "nombre")]
    [TestCase("activos", "numSerie")]
    [TestCase("activos", "nombreCliente")]
    [TestCase("activos", "nombreModeloMaquina")]
    [TestCase("activos", "estado")]
    [TestCase("modelos-maquina", "nombre")]
    [TestCase("modelos-maquina", "nombreTipoMaquina")]
    [TestCase("modelos-maquina", "descripcion")]
    [TestCase("modelos-maquina", "estado")]
    [TestCase("tipos-maquina", "nombre")]
    [TestCase("tipos-maquina", "estado")]
    [TestCase("repuestos", "codigo")]
    [TestCase("repuestos", "nombre")]
    [TestCase("repuestos", "marca")]
    [TestCase("repuestos", "ubicacion")]
    [TestCase("repuestos", "cantidad")]
    [TestCase("repuestos", "cantidadMinima")]
    [TestCase("repuestos", "estado")]
    [TestCase("movimientos-stock", "fecha")]
    [TestCase("movimientos-stock", "codigoRepuesto")]
    [TestCase("movimientos-stock", "nombreRepuesto")]
    [TestCase("movimientos-stock", "tipo")]
    [TestCase("movimientos-stock", "cantidad")]
    [TestCase("movimientos-stock", "ubicacion")]
    [TestCase("movimientos-stock", "nombreUsuario")]
    [TestCase("movimientos-stock", "motivo")]
    [TestCase("ordenes-trabajo", "id")]
    [TestCase("ordenes-trabajo", "nombreCliente")]
    [TestCase("ordenes-trabajo", "nombreActivo")]
    [TestCase("ordenes-trabajo", "tipo")]
    [TestCase("ordenes-trabajo", "prioridad")]
    [TestCase("ordenes-trabajo", "nombreTecnico")]
    [TestCase("ordenes-trabajo", "estado")]
    [TestCase("ordenes-trabajo", "creadoEn")]
    [TestCase("ordenes-trabajo", "fechaPrevistaEjecucion")]
    [TestCase("documentos-tecnicos", "titulo")]
    [TestCase("documentos-tecnicos", "tipo")]
    [TestCase("documentos-tecnicos", "vigente")]
    [TestCase("documentos-tecnicos", "nombreAsociado")]
    [TestCase("documentos-tecnicos", "usaSugerenciasIA")]
    [TestCase("documentos-tecnicos", "creadoEn")]
    public async Task CadaListadoAceptaSusColumnasOrdenablesEnAmbosSentidos(string recurso, string columna)
    {
        foreach (var sentido in new[] { "asc", "desc" })
        {
            var respuesta = await Cliente.GetAsync($"/api/v1/{recurso}?ordenarPor={columna}&direccionOrden={sentido}");

            Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK), $"{recurso} {columna} {sentido}");
        }
    }

    [TestCase("usuarios")]
    [TestCase("clientes")]
    [TestCase("activos")]
    [TestCase("modelos-maquina")]
    [TestCase("tipos-maquina")]
    [TestCase("repuestos")]
    [TestCase("movimientos-stock")]
    [TestCase("ordenes-trabajo")]
    [TestCase("documentos-tecnicos")]
    public async Task CadaListadoRechazaUnaColumnaQueNoEstaEnSuLista(string recurso)
    {
        var respuesta = await Cliente.GetAsync($"/api/v1/{recurso}?ordenarPor=contrasena");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), recurso);
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("ORDEN_INVALIDO"), recurso);
    }

    [TestCase("usuarios")]
    [TestCase("activos")]
    [TestCase("repuestos")]
    [TestCase("movimientos-stock")]
    [TestCase("ordenes-trabajo")]
    [TestCase("documentos-tecnicos")]
    public async Task UnTextoConSqlComoColumnaSeRechazaSinLlegarALaBase(string recurso)
    {
        var respuesta = await Cliente.GetAsync($"/api/v1/{recurso}?ordenarPor=" + Uri.EscapeDataString("id; DROP TABLE x"));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), recurso);
    }

    [Test]
    public async Task LosUsuariosSeOrdenanPorCorreoYSusPaginasNoSeSolapan()
    {
        var ascendente = await LeerColumnaAsync("/api/v1/usuarios?ordenarPor=correo&direccionOrden=asc&tamanoPagina=3", "correo");
        var descendente = await LeerColumnaAsync("/api/v1/usuarios?ordenarPor=correo&direccionOrden=desc&tamanoPagina=3", "correo");
        var segundaPagina = await LeerColumnaAsync("/api/v1/usuarios?ordenarPor=correo&direccionOrden=asc&tamanoPagina=3&pagina=2", "correo");

        Assert.That(ascendente.First(), Is.Not.EqualTo(descendente.First()));
        Assert.That(ascendente.Intersect(segundaPagina), Is.Empty);
    }

    [Test]
    public async Task LosRepuestosSeOrdenanPorCantidadEnAmbosSentidos()
    {
        var ascendente = (await LeerColumnaAsync("/api/v1/repuestos?ordenarPor=cantidad&direccionOrden=asc&tamanoPagina=50", "cantidad")).Select(int.Parse).ToList();
        var descendente = (await LeerColumnaAsync("/api/v1/repuestos?ordenarPor=cantidad&direccionOrden=desc&tamanoPagina=50", "cantidad")).Select(int.Parse).ToList();

        Assert.That(ascendente, Is.Ordered);
        Assert.That(descendente, Is.Ordered.Descending);
    }

    [Test]
    public async Task LosMovimientosSeOrdenanPorCantidadYPorDefectoLosMasNuevosPrimero()
    {
        var porCantidad = (await LeerColumnaAsync("/api/v1/movimientos-stock?ordenarPor=cantidad&direccionOrden=asc&tamanoPagina=50", "cantidad")).Select(int.Parse).ToList();
        var porDefecto = (await LeerColumnaAsync("/api/v1/movimientos-stock?tamanoPagina=50", "fecha")).Select(DateTime.Parse).ToList();

        Assert.That(porCantidad, Is.Ordered);
        Assert.That(porDefecto, Is.Ordered.Descending);
    }

    [Test]
    public async Task LasOrdenesSePuedenOrdenarPorPrioridadYSigueFuncionandoCreadoEn()
    {
        var prioridades = (await LeerColumnaAsync("/api/v1/ordenes-trabajo?ordenarPor=prioridad&direccionOrden=asc&tamanoPagina=50", "prioridad")).ToList();
        var orden = new[] { "baja", "media", "alta" };
        var indices = prioridades.Select(p => Array.IndexOf(orden, p)).ToList();

        Assert.That(indices, Is.Ordered);
        var porCreacion = await Cliente.GetAsync("/api/v1/ordenes-trabajo?ordenarPor=creadoEn&direccionOrden=desc");
        Assert.That(porCreacion.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}
