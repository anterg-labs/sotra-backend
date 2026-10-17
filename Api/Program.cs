using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Operaciones;
using Api.Servicios;
using Api.Seguridad;

DotNetEnv.Env.TraversePath().Load();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AgregarAutenticacionKeycloak();
builder.Services.AddAuthorization();

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

app.UseRouting();
app.UseCors("FrontendPolicy");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();
