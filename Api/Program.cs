using Api.Infraestructura;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Formatting.Compact;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Operaciones;
using Api.Servicios;
using Api.Seguridad;

DotNetEnv.Env.TraversePath().Load();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((contexto, configuracion) =>
{
    configuracion
        .ReadFrom.Configuration(contexto.Configuration)
        .Enrich.FromLogContext();

    if (contexto.HostingEnvironment.IsDevelopment())
    {
        configuracion.WriteTo.Console();
    }
    else
    {
        configuracion.WriteTo.Console(new CompactJsonFormatter());
    }
});

builder.Services.AgregarManejoDeErrores();
builder.Services.AgregarLimiteDeSolicitudes(builder.Configuration);
builder.Services.AgregarObservabilidad(builder.Configuration);
builder.Services.AgregarProxiesDeConfianza(builder.Configuration);

builder.Services.AddControllers(opciones => opciones.Filters.Add<AuditoriaDeAcciones>())
    .AddJsonOptions(opciones => opciones.JsonSerializerOptions.Converters.Add(new TextoSinCaracteresNulosConverter()));
builder.WebHost.ConfigureKestrel(opciones => opciones.AddServerHeader = false);
builder.Services.AddDbContext<SotraContext>();

builder.Services.AddScoped<UsuarioDAO>();
builder.Services.AddScoped<ClienteDAO>();
builder.Services.AddScoped<TipoMaquinaDAO>();
builder.Services.AddScoped<ModeloMaquinaDAO>();
builder.Services.AddScoped<RolDAO>();
builder.Services.AddScoped<TerminosDAO>();
builder.Services.AddScoped<ActivoDAO>();
builder.Services.AddScoped<RepuestoDAO>();
builder.Services.AddScoped<MovimientoStockDAO>();
builder.Services.AddScoped<OrdenTrabajoDAO>();
builder.Services.AddScoped<AnaliticaDAO>();
builder.Services.AddOptions<OpcionesDeAlertasDeMaquinaria>()
    .Bind(builder.Configuration.GetSection(OpcionesDeAlertasDeMaquinaria.Seccion))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddScoped<EvidenciaOtDAO>();
builder.Services.AddScoped<ServicioOllama>();
builder.Services.AddSingleton<ServicioReranker>();
builder.Services.AddScoped<ServicioDocumentosTecnicos>();
builder.Services.AddScoped<ServicioSugerencias>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UsuarioActual>();
builder.Services.AddScoped<IUsuarioActual>(proveedor => proveedor.GetRequiredService<UsuarioActual>());
builder.Services.AddSingleton<ServicioKeycloakUsuarios>();

var autenticacionObligatoria = PoliticasDeAcceso.AutenticacionObligatoria();
builder.Services.AgregarAutenticacionKeycloak();
builder.Services.AgregarPoliticasDeAcceso(autenticacionObligatoria);

builder.Services.AddHealthChecks()
    .AddCheck<SaludDeLaBaseDeDatos>("base-de-datos", tags: [SaludDeLaBaseDeDatos.EtiquetaDisponibilidad]);

builder.Services.AddOpenApi();

var frontendOrigin = Environment.GetEnvironmentVariable("FRONTEND_ORIGIN");
if (string.IsNullOrWhiteSpace(frontendOrigin))
{
    throw new InvalidOperationException(
        "No se encontró la variable FRONTEND_ORIGIN en el archivo .env.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(frontendOrigin)
              .WithHeaders("Content-Type", "Authorization", "If-Match", CorrelacionMiddleware.Cabecera)
              .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
              .WithExposedHeaders(CorrelacionMiddleware.Cabecera, "Content-Disposition", "Retry-After", "ETag");
    });
});

var app = builder.Build();

if (!autenticacionObligatoria)
{
    app.Logger.LogWarning("AUTH_OBLIGATORIA=false: la API no exige token en los endpoints protegidos.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseMiddleware<CabecerasDeSeguridadMiddleware>();

app.UseMiddleware<CorrelacionMiddleware>();
app.UseSerilogRequestLogging(opciones =>
{
    opciones.EnrichDiagnosticContext = (diagnostico, contexto) =>
        diagnostico.Set("DireccionIp", contexto.Connection.RemoteIpAddress?.ToString());
});
app.UseExceptionHandler();
app.UseMiddleware<EntradaSinCaracteresNulosMiddleware>();
app.UseStatusCodePages();
app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = comprobacion => comprobacion.Tags.Contains(SaludDeLaBaseDeDatos.EtiquetaDisponibilidad),
}).AllowAnonymous();

app.Run();
