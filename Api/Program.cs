using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Operaciones;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<SotraContext>();
builder.Services.AddScoped<UsuarioDAO>();
builder.Services.AddScoped<ClienteDAO>();
builder.Services.AddScoped<TipoMaquinaDAO>();
builder.Services.AddScoped<ModeloMaquinaDAO>();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
