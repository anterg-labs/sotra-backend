using Microsoft.AspNetCore.Authorization;

namespace Api.Seguridad;

// Nombres de las políticas que usan los controladores con [Authorize(Policy = ...)].
// Siguen los nombres de operativo.permiso cuando el permiso existe en la base.
public static class Politicas
{
    public const string UsuarioConsultar = "usuario.consultar";
    public const string UsuarioListar = "usuario.listar";
    public const string UsuarioCrear = "usuario.crear";
    public const string UsuarioEditar = "usuario.editar";

    public const string ClienteConsultar = "cliente.consultar";
    public const string ClienteGestionar = "cliente.gestionar";

    public const string ActivoConsultar = "activo.consultar";
    public const string ActivoCrear = "activo.crear";
    public const string ActivoEditar = "activo.editar";

    public const string ModeloConsultar = "modelo.consultar";
    public const string ModeloCrear = "modelo.crear";
    public const string ModeloEditar = "modelo.editar";

    public const string TipoMaquinaConsultar = "tipo_maquina.consultar";
    public const string TipoMaquinaGestionar = "tipo_maquina.gestionar";

    public const string OtConsultar = "ot.consultar";
    public const string OtCrear = "ot.crear";
    public const string OtEditar = "ot.editar";
    public const string OtAsignarTecnico = "ot.asignar_tecnico";
    public const string OtRegistrarAvance = "ot.registrar_avance";

    public const string DocumentoConsultar = "documento.consultar";
    public const string DocumentoGestionar = "documento.gestionar";

    public const string StockConsultar = "stock.consultar";
    public const string RepuestoGestionar = "repuesto.gestionar";
    public const string StockRegistrarMovimiento = "stock.registrar_movimiento";

    public const string ReporteConsultar = "reporte.consultar";

    public const string TerminosAdministrar = "terminos.administrar";
}

// Roles del realm sotra de Keycloak.
public static class Roles
{
    public const string Administrador = "administrador";
    public const string JefeMantenimiento = "jefe_mantenimiento";
    public const string Tecnico = "tecnico";
    public const string Panolero = "panolero";

    // Rol de Keycloak que corresponde a cada rol de operativo.rol.
    private static readonly Dictionary<string, string> RolPorRolDeSotra = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Administrador"] = Administrador,
        ["Jefe de mantenimiento"] = JefeMantenimiento,
        ["Técnico"] = Tecnico,
        ["Pañolero"] = Panolero
    };

    public static List<string> DesdeRolesDeSotra(IEnumerable<string> nombresDeRoles)
    {
        var roles = new List<string>();
        foreach (var nombre in nombresDeRoles)
        {
            if (RolPorRolDeSotra.TryGetValue(nombre, out var rol))
            {
                roles.Add(rol);
            }
        }

        return roles;
    }
}

public static class PoliticasDeAcceso
{
    // Matriz de acceso: qué roles cumplen cada política.
    private static readonly Dictionary<string, string[]> RolesPorPolitica = new()
    {
        // El jefe también gestiona usuarios, salvo administradores (UsuariosController lo controla).
        [Politicas.UsuarioConsultar] = [Roles.Administrador, Roles.JefeMantenimiento],
        [Politicas.UsuarioListar] = [Roles.Administrador, Roles.JefeMantenimiento],
        [Politicas.UsuarioCrear] = [Roles.Administrador, Roles.JefeMantenimiento],
        [Politicas.UsuarioEditar] = [Roles.Administrador, Roles.JefeMantenimiento],

        [Politicas.ClienteConsultar] = [Roles.Administrador, Roles.JefeMantenimiento, Roles.Tecnico, Roles.Panolero],
        [Politicas.ClienteGestionar] = [Roles.Administrador, Roles.JefeMantenimiento],

        [Politicas.ActivoConsultar] = [Roles.Administrador, Roles.JefeMantenimiento, Roles.Tecnico, Roles.Panolero],
        [Politicas.ActivoCrear] = [Roles.Administrador, Roles.JefeMantenimiento],
        [Politicas.ActivoEditar] = [Roles.Administrador, Roles.JefeMantenimiento],

        [Politicas.ModeloConsultar] = [Roles.Administrador, Roles.JefeMantenimiento, Roles.Tecnico],
        [Politicas.ModeloCrear] = [Roles.Administrador, Roles.JefeMantenimiento],
        [Politicas.ModeloEditar] = [Roles.Administrador, Roles.JefeMantenimiento],

        [Politicas.TipoMaquinaConsultar] = [Roles.Administrador, Roles.JefeMantenimiento, Roles.Tecnico],
        [Politicas.TipoMaquinaGestionar] = [Roles.Administrador, Roles.JefeMantenimiento],

        [Politicas.OtConsultar] = [Roles.Administrador, Roles.JefeMantenimiento, Roles.Tecnico, Roles.Panolero],
        [Politicas.OtCrear] = [Roles.Administrador, Roles.JefeMantenimiento],
        [Politicas.OtEditar] = [Roles.Administrador, Roles.JefeMantenimiento],
        [Politicas.OtAsignarTecnico] = [Roles.Administrador, Roles.JefeMantenimiento],
        [Politicas.OtRegistrarAvance] = [Roles.Administrador, Roles.JefeMantenimiento, Roles.Tecnico],

        [Politicas.DocumentoConsultar] = [Roles.Administrador, Roles.JefeMantenimiento, Roles.Tecnico],
        [Politicas.DocumentoGestionar] = [Roles.Administrador, Roles.JefeMantenimiento],

        [Politicas.StockConsultar] = [Roles.Administrador, Roles.Panolero],
        [Politicas.RepuestoGestionar] = [Roles.Administrador, Roles.Panolero],
        [Politicas.StockRegistrarMovimiento] = [Roles.Administrador, Roles.Panolero],

        [Politicas.ReporteConsultar] = [Roles.Administrador, Roles.JefeMantenimiento],

        [Politicas.TerminosAdministrar] = [Roles.Administrador]
    };

    // Con AUTH_OBLIGATORIA=false la API no exige token en los endpoints con política,
    // para que el frontend funcione mientras integra el login. Si la variable falta o
    // tiene cualquier otro valor, se exige token.
    public static bool AutenticacionObligatoria()
    {
        var valor = Environment.GetEnvironmentVariable("AUTH_OBLIGATORIA");
        return !string.Equals(valor?.Trim(), "false", StringComparison.OrdinalIgnoreCase);
    }

    public static IServiceCollection AgregarPoliticasDeAcceso(this IServiceCollection servicios, bool obligatoria)
    {
        servicios.AddAuthorization(opciones =>
        {
            foreach (var (politica, roles) in RolesPorPolitica)
            {
                opciones.AddPolicy(politica, constructor =>
                {
                    if (obligatoria)
                    {
                        constructor.RequireRole(roles);
                    }
                    else
                    {
                        constructor.RequireAssertion(_ => true);
                    }
                });
            }

            // Un endpoint sin [Authorize] ni [AllowAnonymous] exige sesión: así un
            // endpoint nuevo nunca queda abierto por olvido.
            if (obligatoria)
            {
                opciones.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
            }
        });

        return servicios;
    }
}
