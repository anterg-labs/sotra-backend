using System.Net;
using System.Net.Http.Json;
using NUnit.Framework;

namespace Api.Pruebas.Caracterizacion;

public class ClientesPruebas : PruebaBase
{
    [Test]
    public async Task ListarClientesDevuelveUnaPaginaConElFormatoEstandar()
    {
        await DatosDePrueba.CrearClienteAsync(Cliente);

        var respuesta = await Cliente.GetAsync("/api/v1/clientes");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var pagina = await respuesta.LeerJsonAsync();
        Assert.That(pagina.GetProperty("totalElementos").GetInt32(), Is.GreaterThan(0));
        var propiedades = pagina.GetProperty("elementos")[0].EnumerateObject().Select(p => p.Name).ToList();
        Assert.That(propiedades, Is.EquivalentTo(new[]
        {
            "id", "razonSocial", "nombreContacto", "correo", "telefono", "direccion",
            "observaciones", "cantidadActivosAsociados", "cantidadOrdenesAsociadas", "estado", "creadoEn",
        }));
    }

    [Test]
    public async Task ListarClientesRechazaUnaPaginacionInvalida()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/clientes?pagina=0");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("PAGINACION_INVALIDA"));
    }

    [Test]
    public async Task ListarClientesBuscaPorRazonSocial()
    {
        var razonSocial = "Busqueda " + DatosDePrueba.Sufijo();
        await DatosDePrueba.CrearClienteAsync(Cliente, DatosDePrueba.ClienteNuevo(razonSocial: razonSocial));

        var respuesta = await Cliente.GetAsync("/api/v1/clientes?buscar=" + Uri.EscapeDataString(razonSocial.ToUpper()));

        var pagina = await respuesta.LeerJsonAsync();
        Assert.That(pagina.GetProperty("totalElementos").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task OrdenarClientesPorRazonSocialUsaAlfabetoEspanolAntesDePaginar()
    {
        var prefijo = "Orden " + DatosDePrueba.Sufijo() + " ";
        foreach (var nombre in new[] { "Zorro", "Árbol", "Beto", "abeja" })
        {
            await DatosDePrueba.CrearClienteAsync(Cliente,
                DatosDePrueba.ClienteNuevo(razonSocial: prefijo + nombre));
        }

        async Task<string[]> Pagina(int pagina, string direccion)
        {
            var ruta = "/api/v1/clientes?buscar=" + Uri.EscapeDataString(prefijo)
                + "&ordenarPor=razonSocial&direccionOrden=" + direccion
                + "&pagina=" + pagina + "&tamanoPagina=2";
            var respuesta = await Cliente.GetAsync(ruta);
            respuesta.EnsureSuccessStatusCode();
            var datos = await respuesta.LeerJsonAsync();
            return datos.GetProperty("elementos").EnumerateArray()
                .Select(cliente => cliente.GetProperty("razonSocial").GetString()![prefijo.Length..]).ToArray();
        }

        Assert.That(await Pagina(1, "asc"), Is.EqualTo(new[] { "abeja", "Árbol" }));
        Assert.That(await Pagina(2, "asc"), Is.EqualTo(new[] { "Beto", "Zorro" }));
        Assert.That(await Pagina(1, "desc"), Is.EqualTo(new[] { "Zorro", "Beto" }));
    }

    [Test]
    public async Task ListarClientesFiltraPorEstado()
    {
        var cliente = await DatosDePrueba.CrearClienteAsync(Cliente);
        var id = cliente.GetProperty("id").GetInt64();
        await Cliente.PatchAsJsonAsync("/api/v1/clientes/" + id, new { estado = "inactivo" });

        var respuesta = await Cliente.GetAsync("/api/v1/clientes?estado=inactivo&tamanoPagina=100");

        var pagina = await respuesta.LeerJsonAsync();
        var ids = pagina.GetProperty("elementos").EnumerateArray().Select(c => c.GetProperty("id").GetInt64());
        Assert.That(ids, Does.Contain(id));
        var estados = pagina.GetProperty("elementos").EnumerateArray().Select(c => c.GetProperty("estado").GetString()).Distinct();
        Assert.That(estados, Is.EqualTo(new[] { "inactivo" }));
    }

    [Test]
    public async Task ObtenerUnClienteInexistenteResponde404()
    {
        var respuesta = await Cliente.GetAsync("/api/v1/clientes/999999999");

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CrearClienteResponde201ConLocationYEstadoActivo()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/clientes", DatosDePrueba.ClienteNuevo());

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var cliente = await respuesta.LeerJsonAsync();
        var id = cliente.GetProperty("id").GetInt64();
        Assert.That(respuesta.Headers.Location!.ToString(), Does.EndWith("/api/v1/clientes/" + id));
        Assert.That(cliente.GetProperty("estado").GetString(), Is.EqualTo("activo"));
        Assert.That(cliente.GetProperty("cantidadActivosAsociados").GetInt32(), Is.EqualTo(0));
    }

    [Test]
    public async Task CrearClienteSoloConLosCamposObligatoriosResponde201()
    {
        var datos = new { razonSocial = "Minima " + DatosDePrueba.Sufijo(), nombreContacto = "Contacto " + DatosDePrueba.Sufijo() };

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/clientes", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task CrearClienteSinLosCamposRequeridosResponde400PorErrorDeDeserializacion()
    {
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/clientes", new { correo = "a@pruebas.test" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        var campos = cuerpo.GetProperty("errors").EnumerateObject().Select(p => p.Name).ToList();
        Assert.That(campos, Does.Contain("solicitud"));
    }

    [Test]
    public async Task CrearClienteConUnCorreoInvalidoResponde400ConErrorEnCorreo()
    {
        var datos = new { razonSocial = "Cliente " + DatosDePrueba.Sufijo(), nombreContacto = "Contacto", correo = "no-es-un-correo" };

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/clientes", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("errors").EnumerateObject().Select(p => p.Name), Does.Contain("Correo"));
    }

    [Test]
    public async Task CrearClienteConRazonSocialRepetidaResponde409()
    {
        var primero = await DatosDePrueba.CrearClienteAsync(Cliente);
        var razonSocial = primero.GetProperty("razonSocial").GetString();

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/clientes", DatosDePrueba.ClienteNuevo(razonSocial: razonSocial));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("RAZON_SOCIAL_DUPLICADA"));
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("El nombre o razón social ya está registrado."));
    }

    [Test]
    public async Task ActualizarClienteReemplazaLosCampos()
    {
        var cliente = await DatosDePrueba.CrearClienteAsync(Cliente);
        var id = cliente.GetProperty("id").GetInt64();
        var datos = DatosDePrueba.ClienteNuevo(razonSocial: "Renombrada " + DatosDePrueba.Sufijo(), nombreContacto: "Otro contacto");

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/clientes/" + id, datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var actualizado = await respuesta.LeerJsonAsync();
        Assert.That(actualizado.GetProperty("nombreContacto").GetString(), Is.EqualTo("Otro contacto"));
    }

    [Test]
    public async Task ActualizarClienteInexistenteResponde404()
    {
        var datos = DatosDePrueba.ClienteNuevo(razonSocial: "X " + DatosDePrueba.Sufijo(), nombreContacto: "Y");

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/clientes/999999999", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ActualizarClienteConUnaRazonSocialDeOtroResponde409()
    {
        var primero = await DatosDePrueba.CrearClienteAsync(Cliente);
        var segundo = await DatosDePrueba.CrearClienteAsync(Cliente);
        var datos = DatosDePrueba.ClienteNuevo(razonSocial: primero.GetProperty("razonSocial").GetString(), nombreContacto: "Contacto");

        var respuesta = await Cliente.PutAsJsonAsync("/api/v1/clientes/" + segundo.GetProperty("id").GetInt64(), datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task CambiarEstadoDesactivaYReactivaUnCliente()
    {
        var cliente = await DatosDePrueba.CrearClienteAsync(Cliente);
        var id = cliente.GetProperty("id").GetInt64();

        var baja = await Cliente.PatchAsJsonAsync("/api/v1/clientes/" + id, new { estado = "inactivo" });
        var alta = await Cliente.PatchAsJsonAsync("/api/v1/clientes/" + id, new { estado = "activo" });

        Assert.That((await baja.LeerJsonAsync()).GetProperty("estado").GetString(), Is.EqualTo("inactivo"));
        Assert.That((await alta.LeerJsonAsync()).GetProperty("estado").GetString(), Is.EqualTo("activo"));
    }

    [Test]
    public async Task CambiarEstadoConUnValorInvalidoResponde400ConMensaje()
    {
        var cliente = await DatosDePrueba.CrearClienteAsync(Cliente);

        var respuesta = await Cliente.PatchAsJsonAsync(
            "/api/v1/clientes/" + cliente.GetProperty("id").GetInt64(), new { estado = "suspendido" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("El estado debe ser 'activo' o 'inactivo'."));
    }

    [Test]
    public async Task CambiarEstadoDeUnClienteInexistenteResponde404()
    {
        var respuesta = await Cliente.PatchAsJsonAsync("/api/v1/clientes/999999999", new { estado = "inactivo" });

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task CrearClienteRechazaUnTelefonoConLetras()
    {
        var datos = DatosDePrueba.ClienteNuevo(telefono: "abc" + DatosDePrueba.Telefono());

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/clientes", datos);

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task CrearClienteConTelefonoRepetidoResponde409()
    {
        var primero = await DatosDePrueba.CrearClienteAsync(Cliente);
        var telefono = primero.GetProperty("telefono").GetString();

        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/clientes", DatosDePrueba.ClienteNuevo(telefono: telefono));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var cuerpo = await respuesta.LeerJsonAsync();
        Assert.That(cuerpo.GetProperty("codigo").GetString(), Is.EqualTo("TELEFONO_DUPLICADO"));
        Assert.That(cuerpo.GetProperty("mensaje").GetString(), Is.EqualTo("El número telefónico ya está registrado."));
    }

    [Test]
    public async Task AltaConCeroInicialConservaElFormatoEnAltaDetalleYListado()
    {
        var telefono = "0" + DatosDePrueba.Telefono();
        var respuesta = await Cliente.PostAsJsonAsync("/api/v1/clientes", DatosDePrueba.ClienteNuevo(telefono: telefono));

        Assert.That(respuesta.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var cliente = await respuesta.LeerJsonAsync();
        Assert.That(cliente.GetProperty("telefono").GetString(), Is.EqualTo(telefono));

        var id = cliente.GetProperty("id").GetInt64();
        var detalle = await (await Cliente.GetAsync($"/api/v1/clientes/{id}")).LeerJsonAsync();
        Assert.That(detalle.GetProperty("telefono").GetString(), Is.EqualTo(telefono));

        var correo = cliente.GetProperty("correo").GetString();
        var listado = await (await Cliente.GetAsync("/api/v1/clientes?buscar=" + Uri.EscapeDataString(correo!))).LeerJsonAsync();
        Assert.That(listado.GetProperty("elementos")[0].GetProperty("telefono").GetString(), Is.EqualTo(telefono));
    }

    [Test]
    public async Task TelefonosConYSinCeroInicialNoPuedenDuplicarse()
    {
        var telefono = DatosDePrueba.Telefono();
        var primero = await Cliente.PostAsJsonAsync("/api/v1/clientes", DatosDePrueba.ClienteNuevo(telefono: "0" + telefono));
        Assert.That(primero.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        var repetido = await Cliente.PostAsJsonAsync("/api/v1/clientes", DatosDePrueba.ClienteNuevo(telefono: telefono));
        Assert.That(repetido.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That((await repetido.LeerJsonAsync()).GetProperty("codigo").GetString(), Is.EqualTo("TELEFONO_DUPLICADO"));
    }
}
