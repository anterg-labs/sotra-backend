var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => "SOTRA Backend");

app.Run();