using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class ActivosPruebas : PruebaBase
{
    [Test]
    public async Task ListarActivosDevuelveUnaPaginaConElFormatoDelContrato()
    {
        await DatosDePrueba.CrearActivoAsync(Cliente);

        var respuesta = await Cliente.GetAsync("/api/v1/activos");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var pagina = await respuesta.LeerJsonAsync();
        var propiedades = pagina.GetProperty("elementos")[0].EnumerateObject().Select(p => p.Name).ToList();
        Assert.That(propiedades, Is.EquivalentTo(new[]
        {
            "id", "clienteId", "nombreCliente", "numSerie", "descripcion", "modeloMaquinaId", "nombreModeloMaquina",
            "tipoMaquinaId", "nombreTipoMaquina", "anioFabricacion", "nombre", "ubicacion", "unidadUso", "estado",
        }));
        Assert.That(pagina.GetProperty("elementos")[0].GetProperty("nombreCliente").GetString(), Is.Not.Empty);
        Assert.That(pagina.GetProperty("elementos")[0].GetProperty("nombreModeloMaquina").GetString(), Is.Not.Empty);
        Assert.That(pagina.GetProperty("elementos")[0].GetProperty("nombreTipoMaquina").GetString(), Is.Not.Empty);
    }

    [Test]
    public async Task ListarActivosBuscaPorTipoDeMaquinaYModelo()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente, "Grúa " + DatosDePrueba.Sufijo());
        var modelo = await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipo.GetProperty("id").GetInt64());
        var cliente = await DatosDePrueba.CrearClienteAsync(Cliente);
        var numSerie = "SERIE-" + DatosDePrueba.Sufijo();
        await Cliente.PostAsJsonAsync("/api/v1/activos",
            DatosDePrueba.ActivoNuevo(cliente.GetProperty("id").GetInt64(), modelo.GetProperty("id").GetInt64(), numSerie));

        var porTipo = await (await Cliente.GetAsync("/api/v1/activos?buscar=" + Uri.EscapeDataString(tipo.GetProperty("nombre").GetString()!))).LeerJsonAsync();
        var porModelo = await (await Cliente.GetAsync("/api/v1/activos?buscar=" + Uri.EscapeDataString(modelo.GetProperty("nombre").GetString()!))).LeerJsonAsync();

        Assert.That(porTipo.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
        Assert.That(porTipo.GetProperty("elementos")[0].GetProperty("nombreTipoMaquina").GetString(), Is.EqualTo(tipo.GetProperty("nombre").GetString()));
        Assert.That(porTipo.GetProperty("elementos")[0].GetProperty("tipoMaquinaId").GetInt64(), Is.EqualTo(tipo.GetProperty("id").GetInt64()));
        Assert.That(porModelo.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task ListarActivosRechazaUnaPaginacionInvalida()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/activos?tamanoPagina=101");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ListarActivosFiltraPorCliente()
    {
        var (clienteId, modeloId) = await DatosDePrueba.CrearClienteYModeloAsync(Cliente);
        await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(clienteId, modeloId));
        await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(clienteId, modeloId));

        var respuesta = await Cliente.GetAsync("/api/v1/activos?clienteId=" + clienteId);

        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("totalElementos").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task ListarActivosBuscaPorNumeroDeSerie()
    {
        var (clienteId, modeloId) = await DatosDePrueba.CrearClienteYModeloAsync(Cliente);
        var numSerie = "SERIE-" + DatosDePrueba.Sufijo();
        await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(clienteId, modeloId, numSerie));

        var respuesta = await Cliente.GetAsync("/api/v1/activos?buscar=" + Uri.EscapeDataString(numSerie.ToLower()));

        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task ObtenerUnActivoInexistenteResponde404()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/activos/999999999");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CrearActivoResponde201ConEstadoActivo()
    {
        var (clienteId, modeloId) = await DatosDePrueba.CrearClienteYModeloAsync(Cliente);

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(clienteId, modeloId));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var activo = await respuesta.LeerJsonAsync();
        Assert.That(activo.GetProperty("estado").GetString(), Is.EqualTo("activo"));
        Assert.That(respuesta.Headers.Location!.ToString(), Does.EndWith("/api/v1/activos/" + activo.GetProperty("id").GetInt64()));
    }

    [Test]
    public async Task CrearActivoConNumeroDeSerieRepetidoResponde409()
    {
        var (clienteId, modeloId) = await DatosDePrueba.CrearClienteYModeloAsync(Cliente);
        var numSerie = "SN-" + DatosDePrueba.Sufijo();
        await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(clienteId, modeloId, numSerie));

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(clienteId, modeloId, numSerie));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("mensaje").GetString(),
            Is.EqualTo("Ya existe un activo con ese número de serie."));
    }

    [Test]
    public async Task CrearActivoSinClienteOModeloResponde400PorElRangoDeLosIdentificadores()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/activos", new { numSerie = "SN-" + DatosDePrueba.Sufijo() });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CrearActivoConUnClienteInexistenteResponde400()
    {
        var (_, modeloId) = await DatosDePrueba.CrearClienteYModeloAsync(Cliente);

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/activos", DatosDePrueba.ActivoNuevo(999999999, modeloId));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CrearActivoConUnModeloInexistenteResponde400()
    {
        var propietario = await DatosDePrueba.CrearClienteAsync(Cliente);

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/activos",
            DatosDePrueba.ActivoNuevo(propietario.GetProperty("id").GetInt64(), 999999999));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ActualizarActivoCambiaLosCamposEnviados()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);
        var id = activo.GetProperty("id").GetInt64();

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/activos/" + id,
            new
            {
                clienteId = activo.GetProperty("clienteId").GetInt64(),
                modeloMaquinaId = activo.GetProperty("modeloMaquinaId").GetInt64(),
                numSerie = activo.GetProperty("numSerie").GetString(),
                nombre = "Nombre actualizado",
                ubicacion = "Planta 2",
                descripcion = (string?)null,
                anioFabricacion = (short?)null,
                estado = "activo",
            });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualizado = await respuesta.LeerJsonAsync();
        Assert.That(actualizado.GetProperty("nombre").GetString(), Is.EqualTo("Nombre actualizado"));
        Assert.That(actualizado.GetProperty("ubicacion").GetString(), Is.EqualTo("Planta 2"));
    }

    [Test]
    public async Task ActualizarActivoInexistenteResponde404()
    {
        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/activos/999999999", new
        {
            clienteId = 1,
            modeloMaquinaId = 1,
            numSerie = "SN-X",
            nombre = "X",
            estado = "activo",
            descripcion = (string?)null,
            ubicacion = (string?)null,
            anioFabricacion = (short?)null,
        });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DesactivarActivoResponde200ConEstadoInactivo()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);

        var respuesta = await Cliente.PatchAsJsonAsync(
            "/api/v1/activos/" + activo.GetProperty("id").GetInt64(), new { estado = "inactivo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("estado").GetString(), Is.EqualTo("inactivo"));
    }

    [Test]
    public async Task DesactivarActivoConOtroEstadoResponde400()
    {
        var activo = await DatosDePrueba.CrearActivoAsync(Cliente);

        var respuesta = await Cliente.PatchAsJsonAsync(
            "/api/v1/activos/" + activo.GetProperty("id").GetInt64(), new { estado = "activo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task DesactivarActivoInexistenteResponde404()
    {
        var respuesta = await Cliente.PatchAsJsonAsync("/api/v1/activos/999999999", new { estado = "inactivo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
