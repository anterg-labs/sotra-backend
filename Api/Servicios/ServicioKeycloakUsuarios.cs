using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.Servicios;

public class ExcepcionKeycloak : Exception
{
    public ExcepcionKeycloak(string mensaje) : base(mensaje)
    {
    }
}

/// <summary>Keycloak no pudo enviar el correo (servidor SMTP sin configurar, credenciales rechazadas, etc.).</summary>
public class ExcepcionCorreoNoEnviado : ExcepcionKeycloak
{
    public ExcepcionCorreoNoEnviado(string mensaje) : base(mensaje)
    {
    }
}

// Datos de un usuario que SOTRA copia a Keycloak para el login.
public record DatosUsuarioKeycloak(string Correo, string Nombre, string Apellido, bool Habilitado);

// Gestiona los usuarios del realm sotra con la API de administración de Keycloak.
// Se autentica como la cuenta de servicio de sotra-backend-admin, que solo puede
// gestionar usuarios (manage-users y view-users de realm-management).
// Los métodos son virtuales para que las pruebas de integración lo reemplacen sin Keycloak.
public class ServicioKeycloakUsuarios
{
    private const string AccionCambiarContrasena = "UPDATE_PASSWORD";
    private const int SegundosDeValidezDelCorreoDeContrasena = 3 * 24 * 60 * 60;
    private const int SegundosDeMargenAntesDeVencerElToken = 30;

    private static readonly HashSet<string> RolesDeSotra = new()
    {
        "administrador", "jefe_mantenimiento", "tecnico", "panolero"
    };

    private readonly HttpClient _cliente = new();
    private readonly SemaphoreSlim _bloqueoDelToken = new(1, 1);
    private readonly ILogger<ServicioKeycloakUsuarios> _registro;
    private string? _token;
    private DateTime _vencimientoDelToken;

    public ServicioKeycloakUsuarios(ILogger<ServicioKeycloakUsuarios> registro)
    {
        _registro = registro;
    }

    // Crea el usuario sin contraseña y marcado para que la elija en el primer ingreso.
    // Si ya existe un usuario con ese correo, devuelve el existente y Creado = false.
    public virtual async Task<(Guid Id, bool Creado)> CrearOObtenerAsync(DatosUsuarioKeycloak datos,
        CancellationToken cancellationToken)
    {
        var existente = await BuscarPorCorreoAsync(datos.Correo, cancellationToken);
        if (existente is not null)
        {
            await ActualizarAsync(existente.Value, datos, cancellationToken);
            return (existente.Value, false);
        }

        var cuerpo = new
        {
            username = datos.Correo,
            email = datos.Correo,
            firstName = datos.Nombre,
            lastName = datos.Apellido,
            enabled = datos.Habilitado,
            emailVerified = true,
            requiredActions = new[] { AccionCambiarContrasena }
        };
        using var respuesta = await EnviarAsync(HttpMethod.Post, "users", cuerpo, cancellationToken);
        await VerificarAsync(respuesta, "crear el usuario");

        var ubicacion = respuesta.Headers.Location?.ToString() ?? "";
        if (!Guid.TryParse(ubicacion[(ubicacion.LastIndexOf('/') + 1)..], out var id))
        {
            throw new ExcepcionKeycloak("Keycloak no devolvió el identificador del usuario creado.");
        }

        return (id, true);
    }

    public virtual async Task ActualizarAsync(Guid id, DatosUsuarioKeycloak datos, CancellationToken cancellationToken)
    {
        // Con "email como usuario" el nombre de usuario debe seguir al correo.
        var cuerpo = new
        {
            username = datos.Correo,
            email = datos.Correo,
            firstName = datos.Nombre,
            lastName = datos.Apellido,
            enabled = datos.Habilitado,
            emailVerified = true
        };
        using var respuesta = await EnviarAsync(HttpMethod.Put, $"users/{id}", cuerpo, cancellationToken);
        await VerificarAsync(respuesta, "actualizar el usuario");
    }

    // Deja en Keycloak exactamente estos roles de SOTRA. Los roles propios de Keycloak
    // (default-roles-sotra, etc.) no se tocan.
    public virtual async Task ReemplazarRolesAsync(Guid id, IEnumerable<string> rolesDeseados,
        CancellationToken cancellationToken)
    {
        var deseados = rolesDeseados.Where(RolesDeSotra.Contains).ToHashSet();
        var actuales = await LeerListaAsync($"users/{id}/role-mappings/realm", cancellationToken);
        var actualesDeSotra = actuales.Where(rol => RolesDeSotra.Contains(Nombre(rol))).ToList();

        var aQuitar = actualesDeSotra.Where(rol => !deseados.Contains(Nombre(rol))).ToList();
        if (aQuitar.Count > 0)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Delete, $"users/{id}/role-mappings/realm", aQuitar,
                cancellationToken);
            await VerificarAsync(respuesta, "quitar roles");
        }

        var nombresActuales = actualesDeSotra.Select(Nombre).ToHashSet();
        var faltantes = deseados.Where(nombre => !nombresActuales.Contains(nombre)).ToHashSet();
        if (faltantes.Count == 0)
        {
            return;
        }

        // Los roles se buscan entre los asignables al usuario: consultar /roles exige
        // view-realm, un permiso que sotra-backend-admin no tiene a propósito.
        var asignables = await LeerListaAsync($"users/{id}/role-mappings/realm/available", cancellationToken);
        var aAgregar = asignables.Where(rol => faltantes.Contains(Nombre(rol))).ToList();
        if (aAgregar.Count != faltantes.Count)
        {
            throw new ExcepcionKeycloak("Algún rol de SOTRA no existe en el realm de Keycloak.");
        }

        if (aAgregar.Count > 0)
        {
            using var respuesta = await EnviarAsync(HttpMethod.Post, $"users/{id}/role-mappings/realm", aAgregar,
                cancellationToken);
            await VerificarAsync(respuesta, "asignar roles");
        }
    }

    // Envía el correo con el enlace para elegir una contraseña nueva. Al terminar, el
    // usuario vuelve al frontend.
    public virtual async Task EnviarCorreoParaElegirContrasenaAsync(Guid id, CancellationToken cancellationToken)
    {
        var parametros = $"lifespan={SegundosDeValidezDelCorreoDeContrasena}";
        var clienteFrontend = Environment.GetEnvironmentVariable("KEYCLOAK_CLIENTE_FRONTEND");
        var origenFrontend = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN");
        if (!string.IsNullOrWhiteSpace(clienteFrontend) && !string.IsNullOrWhiteSpace(origenFrontend))
        {
            parametros += $"&client_id={Uri.EscapeDataString(clienteFrontend)}" +
                          $"&redirect_uri={Uri.EscapeDataString(origenFrontend.TrimEnd('/') + "/")}";
        }

        using var respuesta = await EnviarAsync(HttpMethod.Put, $"users/{id}/execute-actions-email?{parametros}",
            new[] { AccionCambiarContrasena }, cancellationToken);
        try
        {
            await VerificarAsync(respuesta, "enviar el correo para elegir la contraseña");
        }
        catch (ExcepcionKeycloak excepcion)
        {
            throw new ExcepcionCorreoNoEnviado(excepcion.Message);
        }
    }

    public virtual async Task CerrarSesionesAsync(Guid id, CancellationToken cancellationToken)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Post, $"users/{id}/logout", null, cancellationToken);
        await VerificarAsync(respuesta, "cerrar las sesiones del usuario");
    }

    // Se usa para deshacer un alta cuando falla la base. No lanza excepción: si no se
    // puede borrar, lo deja registrado en el log.
    public virtual async Task EliminarSinErrorAsync(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            using var respuesta = await EnviarAsync(HttpMethod.Delete, $"users/{id}", null, cancellationToken);
            await VerificarAsync(respuesta, "eliminar el usuario");
        }
        catch (Exception excepcion)
        {
            _registro.LogError(excepcion, "No se pudo eliminar de Keycloak el usuario {IdKeycloak}.", id);
        }
    }

    private async Task<Guid?> BuscarPorCorreoAsync(string correo, CancellationToken cancellationToken)
    {
        var usuarios = await LeerListaAsync($"users?email={Uri.EscapeDataString(correo)}&exact=true",
            cancellationToken);
        foreach (var usuario in usuarios)
        {
            if (usuario.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out var guid))
            {
                return guid;
            }
        }

        return null;
    }

    private async Task<List<JsonElement>> LeerListaAsync(string ruta, CancellationToken cancellationToken)
    {
        using var respuesta = await EnviarAsync(HttpMethod.Get, ruta, null, cancellationToken);
        await VerificarAsync(respuesta, "consultar Keycloak");
        var elementos = await respuesta.Content.ReadFromJsonAsync<List<JsonElement>>(cancellationToken);
        return elementos ?? new List<JsonElement>();
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string ruta, object? cuerpo,
        CancellationToken cancellationToken)
    {
        var configuracion = LeerConfiguracion();
        var token = await ObtenerTokenAsync(configuracion, cancellationToken);
        var pedido = new HttpRequestMessage(metodo,
            $"{configuracion.Url}/admin/realms/{configuracion.Realm}/{ruta}");
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (cuerpo is not null)
        {
            pedido.Content = JsonContent.Create(cuerpo);
        }

        try
        {
            return await _cliente.SendAsync(pedido, cancellationToken);
        }
        catch (HttpRequestException excepcion)
        {
            throw new ExcepcionKeycloak($"No se pudo conectar con Keycloak: {excepcion.Message}");
        }
    }

    private async Task<string> ObtenerTokenAsync(Configuracion configuracion, CancellationToken cancellationToken)
    {
        await _bloqueoDelToken.WaitAsync(cancellationToken);
        try
        {
            if (_token is not null && DateTime.UtcNow < _vencimientoDelToken)
            {
                return _token;
            }

            var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = configuracion.Cliente,
                ["client_secret"] = configuracion.Secreto
            });
            HttpResponseMessage respuesta;
            try
            {
                respuesta = await _cliente.PostAsync(
                    $"{configuracion.Url}/realms/{configuracion.Realm}/protocol/openid-connect/token",
                    formulario, cancellationToken);
            }
            catch (HttpRequestException excepcion)
            {
                throw new ExcepcionKeycloak($"No se pudo conectar con Keycloak: {excepcion.Message}");
            }

            using (respuesta)
            {
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw new ExcepcionKeycloak(
                        "Keycloak rechazó las credenciales de sotra-backend-admin. Revisá KEYCLOAK_ADMIN_SECRETO.");
                }

                var contenido = await respuesta.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                _token = contenido.GetProperty("access_token").GetString();
                var segundos = contenido.GetProperty("expires_in").GetInt32();
                _vencimientoDelToken = DateTime.UtcNow.AddSeconds(segundos - SegundosDeMargenAntesDeVencerElToken);
                return _token!;
            }
        }
        finally
        {
            _bloqueoDelToken.Release();
        }
    }

    private static async Task VerificarAsync(HttpResponseMessage respuesta, string accion)
    {
        if (respuesta.IsSuccessStatusCode)
        {
            return;
        }

        var detalle = await respuesta.Content.ReadAsStringAsync();
        if (respuesta.StatusCode == HttpStatusCode.Conflict)
        {
            throw new ExcepcionKeycloak($"Keycloak no pudo {accion}: ya existe un usuario con ese correo.");
        }

        throw new ExcepcionKeycloak($"Keycloak no pudo {accion} ({(int)respuesta.StatusCode}): {detalle}");
    }

    private static string Nombre(JsonElement rol)
    {
        return rol.TryGetProperty("name", out var nombre) ? nombre.GetString() ?? "" : "";
    }

    private record Configuracion(string Url, string Realm, string Cliente, string Secreto);

    // Se lee en cada uso para que la API pueda arrancar sin estas variables: solo fallan
    // las operaciones de usuarios, con un mensaje claro.
    private static Configuracion LeerConfiguracion()
    {
        var url = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_URL");
        var realm = Environment.GetEnvironmentVariable("KEYCLOAK_REALM");
        var cliente = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_CLIENTE");
        var secreto = Environment.GetEnvironmentVariable("KEYCLOAK_ADMIN_SECRETO");
        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(realm) ||
            string.IsNullOrWhiteSpace(cliente) || string.IsNullOrWhiteSpace(secreto))
        {
            throw new ExcepcionKeycloak(
                "Falta configurar KEYCLOAK_ADMIN_URL, KEYCLOAK_REALM, KEYCLOAK_ADMIN_CLIENTE o KEYCLOAK_ADMIN_SECRETO.");
        }

        return new Configuracion(url.TrimEnd('/'), realm, cliente, secreto);
    }
}
