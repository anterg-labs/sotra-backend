using System.Text.Json;
using System.Text.Json.Nodes;
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

                // Identificador estable de la operación (`clientes_obtenerPorId`), útil para generar clientes de la API.
                if (contexto.Description.ActionDescriptor is Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor accion)
                {
                    operacion.OperationId = char.ToLowerInvariant(accion.ControllerName[0]) + accion.ControllerName[1..]
                        + "_" + char.ToLowerInvariant(accion.ActionName[0]) + accion.ActionName[1..];
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

                DocumentarConcurrencia(operacion, contexto.Description);
                AgregarEjemplos(operacion, contexto.Description);

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

    // Ejemplos de los flujos principales; las claves son «MÉTODO ruta».
    private static readonly Dictionary<string, (string? Solicitud, string? Respuesta)> Ejemplos = new()
    {
        ["GET api/v1/sesion"] = (null, """{ "idKeycloak": "7c3b1f1e-5c0a-4a53-9b53-1d6c9a7f2b10", "idUsuario": 4, "correo": "admin.prueba@sotra.test", "nombre": "Admin Prueba", "roles": ["administrador"] }"""),
        ["GET api/v1/perfil"] = (null, """{ "id": 4, "nombre": "Admin", "apellido": "Prueba", "correo": "admin.prueba@sotra.test", "telefono": "099123456", "estado": "activo", "fechaIngreso": "2026-10-17", "cantidadOrdenesAsignadas": 0, "roles": [{ "id": 1, "nombre": "Administrador" }] }"""),
        ["PUT api/v1/perfil"] = ("""{ "nombre": "Ana", "apellido": "Pérez", "telefono": "099123456" }""", null),
        ["POST api/v1/ordenes-trabajo"] = ("""{ "clienteId": 3, "activoId": 12, "tecnicoId": 7, "tipo": "correctivo", "prioridad": "alta", "descripcion": "Pérdida de aceite en el cilindro del brazo.", "observaciones": "", "fechaPrevistaEjecucion": "2026-11-05", "repuestos": [{ "repuestoId": 5, "cantidad": 2 }], "sugerencias": [] }""", null),
        ["PATCH api/v1/ordenes-trabajo/{id}/estado"] = ("""{ "estado": "en_progreso", "motivo": null }""", null),
        ["POST api/v1/repuestos/{id}/ingresos"] = ("""{ "cantidad": 10, "observaciones": "Compra a proveedor, factura 1234." }""", null),
    };

    private static void AgregarEjemplos(OpenApiOperation operacion, Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription descripcion)
    {
        if (!Ejemplos.TryGetValue(descripcion.HttpMethod + " " + descripcion.RelativePath, out var ejemplo))
        {
            return;
        }

        if (ejemplo.Solicitud is not null && operacion.RequestBody?.Content?.TryGetValue("application/json", out var cuerpo) == true)
        {
            cuerpo.Example = JsonNode.Parse(ejemplo.Solicitud);
        }

        if (ejemplo.Respuesta is not null
            && operacion.Responses!.TryGetValue("200", out var correcta) && correcta is OpenApiResponse respuesta
            && respuesta.Content?.TryGetValue("application/json", out var contenido) == true)
        {
            contenido.Example = JsonNode.Parse(ejemplo.Respuesta);
        }
    }

    /// <summary>'+N+'    /// Concurrencia optimista de las órdenes: <c>GET</c> y <c>PUT /ordenes-trabajo/{id}</c> devuelven el <c>ETag</c> con la versión
    /// y el <c>PUT</c> acepta <c>If-Match</c> (la versión leída, o <c>*</c>); si cambió responde <c>412</c>.
    /// </summary>
    private static void DocumentarConcurrencia(OpenApiOperation operacion, Microsoft.AspNetCore.Mvc.ApiExplorer.ApiDescription descripcion)
    {
        if (descripcion.RelativePath != "api/v1/ordenes-trabajo/{id}" || descripcion.HttpMethod is not ("GET" or "PUT"))
        {
            return;
        }

        if (operacion.Responses!.TryGetValue("200", out var correcta) && correcta is OpenApiResponse respuesta)
        {
            respuesta.Headers ??= new Dictionary<string, IOpenApiHeader>();
            respuesta.Headers["ETag"] = new OpenApiHeader
            {
                Description = "Versión de la orden entre comillas; se envía en `If-Match` al modificarla.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            };
        }

        if (descripcion.HttpMethod == "PUT")
        {
            operacion.Parameters ??= [];
            operacion.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Versión de la orden que se leyó (valor del `ETag`), o `*` para no comprobarla. Si la orden cambió, responde `412 ORDEN_MODIFICADA`.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            AgregarRespuesta(operacion, "412", "La orden cambió desde que se leyó (`ORDEN_MODIFICADA`); hay que volver a cargarla.");
        }
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
