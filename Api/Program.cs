using Api.Infraestructura;
using Serilog;
using Serilog.Formatting.Compact;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Operaciones;
using Api.Servicios;

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

builder.Services.AddControllers();
builder.Services.AddDbContext<SotraContext>();

builder.Services.AddScoped<UsuarioDAO>();
builder.Services.AddScoped<ClienteDAO>();
builder.Services.AddScoped<TipoMaquinaDAO>();
builder.Services.AddScoped<ModeloMaquinaDAO>();
builder.Services.AddScoped<RolDAO>();
builder.Services.AddScoped<ActivoDAO>();
builder.Services.AddScoped<RepuestoDAO>();
builder.Services.AddScoped<MovimientoStockDAO>();
builder.Services.AddScoped<OrdenTrabajoDAO>();
builder.Services.AddScoped<EvidenciaOtDAO>();
builder.Services.AddScoped<ServicioOllama>();
builder.Services.AddSingleton<ServicioReranker>();
builder.Services.AddScoped<ServicioDocumentosTecnicos>();
builder.Services.AddScoped<ServicioSugerencias>();

builder.Services.AddHealthChecks();

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
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseMiddleware<CorrelacionMiddleware>();
app.UseSerilogRequestLogging(opciones =>
{
    opciones.EnrichDiagnosticContext = (diagnostico, contexto) =>
        diagnostico.Set("DireccionIp", contexto.Connection.RemoteIpAddress?.ToString());
});
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
