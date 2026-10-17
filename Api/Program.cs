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
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UsuarioActual>();
builder.Services.AddSingleton<ServicioKeycloakUsuarios>();

var autenticacionObligatoria = PoliticasDeAcceso.AutenticacionObligatoria();
builder.Services.AgregarAutenticacionKeycloak();
builder.Services.AgregarPoliticasDeAcceso(autenticacionObligatoria);

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

if (!autenticacionObligatoria)
{
    app.Logger.LogWarning("AUTH_OBLIGATORIA=false: la API no exige token en los endpoints protegidos.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
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

app.MapHealthChecks("/health").AllowAnonymous();

app.Run();
