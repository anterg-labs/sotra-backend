namespace Api.Infraestructura.Seguridad;

/// <summary>
/// Configuración de la autenticación. Apagada por defecto: con <see cref="Habilitada"/> en falso la API
/// se comporta como si no existiera seguridad. El emisor y la audiencia se toman de las variables
/// URL_KEYCLOAK e ID_CLIENTE_KEYCLOAK y la integración con Keycloak la implementa otra persona del equipo.
/// </summary>
public sealed class OpcionesDeSeguridad
{
    public const string Seccion = "Seguridad";

    public bool Habilitada { get; init; }

    public string? Emisor { get; init; }

    public string? Audiencia { get; init; }

    /// <summary>Nombre de la claim del token que lista los permisos de la persona (a definir con quien integra Keycloak).</summary>
    public string ClaimPermisos { get; init; } = "permisos";

    /// <summary>Nombre de la claim del token con el correo de la persona.</summary>
    public string ClaimCorreo { get; init; } = "email";

    public bool EsValida(out string error)
    {
        error = "";
        if (!Habilitada)
        {
            return true;
        }

        if (!Uri.TryCreate(Emisor, UriKind.Absolute, out var emisor) || emisor.Scheme != Uri.UriSchemeHttps && !emisor.IsLoopback)
        {
            error = "Con la seguridad habilitada, URL_KEYCLOAK debe ser una URL https válida.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Audiencia))
        {
            error = "Con la seguridad habilitada, ID_CLIENTE_KEYCLOAK es obligatoria.";
            return false;
        }

        return true;
    }
}
