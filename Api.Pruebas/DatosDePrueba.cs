using System.Net.Http.Json;
using System.Text.Json;

namespace Api.Pruebas;

public static class DatosDePrueba
{
    public static string Sufijo()
    {
        return Guid.NewGuid().ToString("N")[..10];
    }

    public static string Telefono()
    {
        return Random.Shared.Next(10_000_000, 99_999_999).ToString();
    }

    public static async Task<JsonElement> LeerJsonAsync(this HttpResponseMessage respuesta)
    {
        return await respuesta.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static object UsuarioNuevo(string? correo = null, string? telefono = null, long[]? idsRol = null)
    {
        return new
        {
            nombre = "Prueba",
            apellido = "Usuario",
            correo = correo ?? "prueba." + Sufijo() + "@pruebas.test",
            telefono = telefono ?? Telefono(),
            idsRol = idsRol ?? new long[] { 1 },
        };
    }

    public static object ClienteNuevo(string? razonSocial = null, string? nombreContacto = null,
        string? correo = null, string? telefono = null)
    {
        var sufijo = Sufijo();
        return new
        {
            razonSocial = razonSocial ?? "Empresa de prueba " + sufijo,
            nombreContacto = nombreContacto ?? "Contacto " + sufijo,
            correo = correo ?? "cliente." + sufijo + "@pruebas.test",
            telefono = telefono ?? Telefono(),
            direccion = "Calle de prueba 123",
            observaciones = "Cliente creado por las pruebas automáticas.",
        };
    }

    public static async Task<JsonElement> CrearUsuarioAsync(HttpClient cliente, object? usuario = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/usuarios", usuario ?? UsuarioNuevo());
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.LeerJsonAsync();
    }

    public static async Task<JsonElement> CrearClienteAsync(HttpClient cliente, object? datos = null)
    {
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/clientes", datos ?? ClienteNuevo());
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.LeerJsonAsync();
    }

    public static async Task<JsonElement> CrearTipoMaquinaAsync(HttpClient cliente, string? nombre = null)
    {
        var datos = new { nombre = nombre ?? "Tipo de prueba " + Sufijo() };
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/tipos-maquina", datos);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.LeerJsonAsync();
    }

    public static async Task<JsonElement> CrearModeloMaquinaAsync(HttpClient cliente, long tipoMaquinaId, string? nombre = null)
    {
        var datos = new
        {
            nombre = nombre ?? "Modelo de prueba " + Sufijo(),
            tipoMaquinaId,
            descripcion = "Modelo creado por las pruebas automáticas.",
        };
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/modelos-maquina", datos);
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.LeerJsonAsync();
    }

    public static async Task<(long ClienteId, long ModeloId)> CrearClienteYModeloAsync(HttpClient cliente)
    {
        var propietario = await CrearClienteAsync(cliente);
        var tipo = await CrearTipoMaquinaAsync(cliente);
        var modelo = await CrearModeloMaquinaAsync(cliente, tipo.GetProperty("id").GetInt64());
        return (propietario.GetProperty("id").GetInt64(), modelo.GetProperty("id").GetInt64());
    }

    public static object ActivoNuevo(long clienteId, long modeloMaquinaId, string? numSerie = null)
    {
        return new
        {
            clienteId,
            numSerie = numSerie ?? "SN-" + Sufijo(),
            modeloMaquinaId,
            nombre = "Activo de prueba",
            ubicacion = "Planta 1",
            descripcion = "Activo creado por las pruebas automáticas.",
            anioFabricacion = (short)2020,
        };
    }

    public static async Task<JsonElement> CrearActivoAsync(HttpClient cliente)
    {
        var (clienteId, modeloId) = await CrearClienteYModeloAsync(cliente);
        var respuesta = await cliente.PostAsJsonAsync("/api/v1/activos", ActivoNuevo(clienteId, modeloId));
        respuesta.EnsureSuccessStatusCode();
        return await respuesta.LeerJsonAsync();
    }
}
