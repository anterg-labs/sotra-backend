using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Api.Infraestructura;

/// <summary>
/// Documento OpenAPI de la API: datos generales, esquema de seguridad Bearer, etiquetas por módulo y las respuestas
/// de error comunes. El documento solo se publica en desarrollo o con <c>OPENAPI_PUBLICAR=true</c> (ver <c>Program.cs</c>).
/// </summary>
public static class DocumentacionOpenApi
{
    public const string EsquemaBearer = "Bearer";
    public const string EsquemaDeError = "ErrorApi";
    public const string RutaDeLaInterfaz = "swagger";

    /// <summary>Política de contenido de la interfaz de Swagger (necesita scripts y estilos propios); no se aplica al resto de la API.</summary>
    public const string PoliticaDeContenidoDeLaInterfaz =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'";

    public static bool EsRutaDeLaInterfaz(PathString ruta) => ruta.StartsWithSegments("/" + RutaDeLaInterfaz);

    /// <summary>En desarrollo siempre; fuera de él, solo con la variable explícita OPENAPI_PUBLICAR=true.</summary>
    public static bool DebePublicarse(IHostEnvironment entorno)
    {
        var variable = Environment.GetEnvironmentVariable("OPENAPI_PUBLICAR");
        return entorno.IsDevelopment() || string.Equals(variable?.Trim(), "true", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly Dictionary<string, string> EtiquetasPorModulo = new()
    {
        ["Activos"] = "Activos (máquinas) de los clientes: alta, consulta, modificación y baja.",
        ["Analitica"] = "Analítica operativa: tendencias, tiempos, repuestos usados, alertas de maquinaria y desempeño.",
        ["Clientes"] = "Clientes de CVM y su actividad asociada.",
        ["DocumentosTecnicos"] = "Documentación técnica (manuales) y consulta asistida.",
        ["Evidencias"] = "Evidencias de una orden de trabajo: fotos, videos y archivos.",
        ["ModelosMaquina"] = "Modelos de máquina del catálogo.",
        ["MovimientosStock"] = "Historial de movimientos de stock de repuestos.",
        ["OrdenesTrabajo"] = "Órdenes de trabajo: ciclo de vida, técnico, repuestos y sugerencias de IA.",
        ["Perfil"] = "Mi perfil: datos propios de la persona con sesión.",
        ["Reportes"] = "Reportes e indicadores (PDF, CSV y tablero).",
        ["Repuestos"] = "Repuestos, stock, reservas e ingresos.",
        ["Roles"] = "Roles de SOTRA.",
        ["Sesion"] = "Sesión actual, según el token de Keycloak.",
        ["Terminos"] = "Términos y condiciones y su aceptación.",
        ["TiposMaquina"] = "Tipos de máquina del catálogo.",
        ["Usuarios"] = "Gestión de usuarios y sus roles.",
        ["Version"] = "Versión de la aplicación (pública).",
    };

    public static IServiceCollection AgregarDocumentacionOpenApi(this IServiceCollection servicios)
    {
        servicios.AddOpenApi(opciones =>
        {
            opciones.AddDocumentTransformer((documento, _, _) =>
            {
                documento.Info = new OpenApiInfo
                {
                    Title = "SOTRA API",
                    Version = LeerVersion(),
                    Description = "API REST de SOTRA (seguimiento de órdenes de trabajo de CVM). Todas las rutas cuelgan de `/api/v1`. "
                        + "Salvo `GET /api/v1/version`, exigen un token Bearer de Keycloak y se restringen por rol. "
                        + "Los errores usan el formato `ProblemDetails` con un campo `codigo` estable.",
                    Contact = new OpenApiContact { Name = "AnterG Labs" },
                };

                documento.Components ??= new OpenApiComponents();
                documento.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                documento.Components.SecuritySchemes[EsquemaBearer] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Token de acceso emitido por Keycloak para el realm `sotra`.",
                };

                documento.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
                documento.Components.Schemas[EsquemaDeError] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Description = "Error de la API (compatible con ProblemDetails, RFC 9457). Nunca incluye trazas ni SQL.",
                    Properties = new Dictionary<string, IOpenApiSchema>
                    {
                        ["codigo"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Código estable del error, por ejemplo `TELEFONO_DUPLICADO`." },
                        ["mensaje"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "Mensaje para mostrar a la persona usuaria." },
                        ["title"] = new OpenApiSchema { Type = JsonSchemaType.String },
                        ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Format = "int32" },
                        ["errors"] = new OpenApiSchema
                        {
                            Type = JsonSchemaType.Object,
                            Description = "Errores de validación por campo.",
                            AdditionalProperties = new OpenApiSchema
                            {
                                Type = JsonSchemaType.Array,
                                Items = new OpenApiSchema { Type = JsonSchemaType.String },
                            },
                        },
                    },
                };

                documento.Tags = EtiquetasPorModulo
                    .Select(par => new OpenApiTag { Name = par.Key, Description = par.Value })
                    .ToHashSet();
                return Task.CompletedTask;
            });

            opciones.AddOperationTransformer((operacion, contexto, _) =>
            {
                // Los parámetros de consulta se publican como se usan en la URL (camelCase: `buscar`, `tamanoPagina`).
                foreach (var parametro in operacion.Parameters?.OfType<OpenApiParameter>() ?? [])
                {
                    if (parametro.In == ParameterLocation.Query && !string.IsNullOrEmpty(parametro.Name))
                    {
                        parametro.Name = char.ToLowerInvariant(parametro.Name[0]) + parametro.Name[1..];
                    }
                }

                var metadatos = contexto.Description.ActionDescriptor.EndpointMetadata;
                var esAnonima = metadatos.OfType<IAllowAnonymous>().Any();
                operacion.Responses ??= new OpenApiResponses();

                if (!esAnonima)
                {
                    operacion.Security ??= [];
                    operacion.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(EsquemaBearer, null)] = [],
                    });
                    AgregarRespuesta(operacion, "401", "Sin sesión o token inválido.");
                    AgregarRespuesta(operacion, "403", "La persona no tiene permiso para esta operación.");
                }

                var tieneEntrada = (operacion.Parameters?.Count ?? 0) > 0 || operacion.RequestBody is not null;
                if (!esAnonima && tieneEntrada)
                {
                    AgregarRespuesta(operacion, "400", "La solicitud no es válida: faltan datos, tienen un formato incorrecto o superan los límites.");
                }

                if (contexto.Description.RelativePath?.Contains('{') == true)
                {
                    AgregarRespuesta(operacion, "404", "El recurso solicitado no existe.");
                }

                AgregarRespuesta(operacion, "429", "Se superó el límite de solicitudes; se informa `Retry-After`.");
                if (operacion.Responses.TryGetValue("429", out var limite) && limite is OpenApiResponse respuestaLimite)
                {
                    respuestaLimite.Headers ??= new Dictionary<string, IOpenApiHeader>();
                    respuestaLimite.Headers["Retry-After"] = new OpenApiHeader
                    {
                        Description = "Segundos que hay que esperar antes de volver a intentar.",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.Integer },
                    };
                }

                AgregarRespuesta(operacion, "500", "Error inesperado del servidor; no incluye detalles técnicos.");
                return Task.CompletedTask;
            });
        });

        return servicios;
    }

    private static void AgregarRespuesta(OpenApiOperation operacion, string codigo, string descripcion)
    {
        if (operacion.Responses!.ContainsKey(codigo))
        {
            return;
        }

        operacion.Responses[codigo] = new OpenApiResponse
        {
            Description = descripcion,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/json"] = new() { Schema = new OpenApiSchemaReference(EsquemaDeError, null) },
            },
        };
    }

    private static string LeerVersion()
    {
        try
        {
            var ruta = Path.Combine(AppContext.BaseDirectory, "version.json");
            using var documento = JsonDocument.Parse(File.ReadAllText(ruta));
            return documento.RootElement.GetProperty("version").GetString() ?? "0.0.0";
        }
        catch (Exception excepcion) when (excepcion is IOException or JsonException or KeyNotFoundException)
        {
            return "0.0.0";
        }
    }
}
