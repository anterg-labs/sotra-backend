using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class TiposYModelosPruebas : PruebaBase
{
    [Test]
    public async Task ListarTiposDevuelveUnaPaginaConIdNombreYEstado()
    {
        await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);

        var respuesta = await Cliente.GetAsync("/api/v1/tipos-maquina");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var pagina = await respuesta.LeerJsonAsync();
        var propiedades = pagina.GetProperty("elementos")[0].EnumerateObject().Select(p => p.Name).ToList();
        Assert.That(propiedades, Is.EquivalentTo(new[] { "id", "nombre", "estado" }));
    }

    [Test]
    public async Task ListarTiposRechazaUnTamanoDePaginaInvalido()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/tipos-maquina?tamanoPagina=0");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ListarTiposBuscaPorNombre()
    {
        var nombre = "Buscable " + DatosDePrueba.Sufijo();
        await DatosDePrueba.CrearTipoMaquinaAsync(Cliente, nombre);

        var respuesta = await Cliente.GetAsync("/api/v1/tipos-maquina?buscar=" + Uri.EscapeDataString(nombre));

        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task CrearTipoResponde201ConEstadoActivo()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/tipos-maquina", new { nombre = "Tipo " + DatosDePrueba.Sufijo() });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var tipo = await respuesta.LeerJsonAsync();
        Assert.That(tipo.GetProperty("estado").GetString(), Is.EqualTo("activo"));
        Assert.That(respuesta.Headers.Location!.ToString(), Does.EndWith("/api/v1/tipos-maquina/" + tipo.GetProperty("id").GetInt64()));
    }

    [Test]
    public async Task CrearTipoConNombreRepetidoResponde409()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/tipos-maquina", new { nombre = tipo.GetProperty("nombre").GetString() });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("mensaje").GetString(),
            Is.EqualTo("Ya existe un tipo de máquina con ese nombre."));
    }

    [Test]
    public async Task CrearTipoConNombreMayorASesentaCaracteresResponde400()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/tipos-maquina", new { nombre = new string('a', 61) });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ObtenerUnTipoInexistenteResponde404()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/tipos-maquina/999999999");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ActualizarTipoCambiaElNombre()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var id = tipo.GetProperty("id").GetInt64();
        var nuevoNombre = "Renombrado " + DatosDePrueba.Sufijo();

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/tipos-maquina/" + id, new { nombre = nuevoNombre });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("nombre").GetString(), Is.EqualTo(nuevoNombre));
    }

    [Test]
    public async Task ActualizarTipoInexistenteResponde404()
    {
        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/tipos-maquina/999999999", new { nombre = "X" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DesactivarTipoResponde200ConEstadoInactivo()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);

        var respuesta = await Cliente.PatchAsJsonAsync(
            "/api/v1/tipos-maquina/" + tipo.GetProperty("id").GetInt64(), new { estado = "inactivo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("estado").GetString(), Is.EqualTo("inactivo"));
    }

    [Test]
    public async Task DesactivarTipoConOtroEstadoResponde400()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);

        var respuesta = await Cliente.PatchAsJsonAsync(
            "/api/v1/tipos-maquina/" + tipo.GetProperty("id").GetInt64(), new { estado = "activo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("mensaje").GetString(),
            Is.EqualTo("Este endpoint solo acepta el estado 'inactivo'."));
    }

    [Test]
    public async Task ListarModelosDevuelveUnaPaginaConElFormatoDelContrato()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipo.GetProperty("id").GetInt64());

        var respuesta = await Cliente.GetAsync("/api/v1/modelos-maquina");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var pagina = await respuesta.LeerJsonAsync();
        var propiedades = pagina.GetProperty("elementos")[0].EnumerateObject().Select(p => p.Name).ToList();
        Assert.That(propiedades, Is.EquivalentTo(new[] { "id", "nombre", "tipoMaquinaId", "nombreTipoMaquina", "descripcion", "estado" }));
        Assert.That(pagina.GetProperty("elementos")[0].GetProperty("nombreTipoMaquina").GetString(), Is.Not.Empty);
    }

    [Test]
    public async Task ListarModelosFiltraPorTipoDeMaquina()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var tipoId = tipo.GetProperty("id").GetInt64();
        await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipoId);
        await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipoId);

        var respuesta = await Cliente.GetAsync("/api/v1/modelos-maquina?tipoMaquinaId=" + tipoId);

        var pagina = await respuesta.LeerJsonAsync();
        Assert.That(pagina.GetProperty("totalElementos").GetInt32(), Is.EqualTo(2));
    }

    [Test]
    public async Task CrearModeloResponde201ConEstadoActivo()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var datos = new { nombre = "Modelo " + DatosDePrueba.Sufijo(), tipoMaquinaId = tipo.GetProperty("id").GetInt64() };

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/modelos-maquina", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("estado").GetString(), Is.EqualTo("activo"));
    }

    [Test]
    public async Task CrearModeloConUnTipoInexistenteResponde400()
    {
        var datos = new { nombre = "Modelo " + DatosDePrueba.Sufijo(), tipoMaquinaId = 999999999L };

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/modelos-maquina", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CrearModeloSinTipoResponde400PorElRangoDelIdentificador()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/modelos-maquina", new { nombre = "Modelo " + DatosDePrueba.Sufijo() });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CrearModeloConNombreRepetidoEnElMismoTipoResponde409()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var tipoId = tipo.GetProperty("id").GetInt64();
        var modelo = await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipoId);

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/modelos-maquina",
            new { nombre = modelo.GetProperty("nombre").GetString(), tipoMaquinaId = tipoId });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task ActualizarModeloCambiaNombreYDescripcion()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var tipoId = tipo.GetProperty("id").GetInt64();
        var modelo = await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipoId);
        var datos = new { nombre = "Nuevo " + DatosDePrueba.Sufijo(), tipoMaquinaId = tipoId, descripcion = "Otra descripción" };

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/modelos-maquina/" + modelo.GetProperty("id").GetInt64(), datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("descripcion").GetString(), Is.EqualTo("Otra descripción"));
    }

    [Test]
    public async Task ActualizarModeloInexistenteResponde404()
    {
        var datos = new { nombre = "X", tipoMaquinaId = 1L };

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/modelos-maquina/999999999", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DesactivarModeloResponde200ConEstadoInactivo()
    {
        var tipo = await DatosDePrueba.CrearTipoMaquinaAsync(Cliente);
        var modelo = await DatosDePrueba.CrearModeloMaquinaAsync(Cliente, tipo.GetProperty("id").GetInt64());

        var respuesta = await Cliente.PatchAsJsonAsync(
            "/api/v1/modelos-maquina/" + modelo.GetProperty("id").GetInt64(), new { estado = "inactivo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That((await respuesta.LeerJsonAsync()).GetProperty("estado").GetString(), Is.EqualTo("inactivo"));
    }

    [Test]
    public async Task DesactivarModeloInexistenteResponde404()
    {
        var respuesta = await Cliente.PatchAsJsonAsync("/api/v1/modelos-maquina/999999999", new { estado = "inactivo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }
}
