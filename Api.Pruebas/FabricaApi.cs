using Api.Servicios;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Pruebas;

public class FabricaApi : WebApplicationFactory<PuntoDeEntrada>
{
    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        constructor.UseEnvironment("Development");
        constructor.ConfigureTestServices(servicios =>
            servicios.AddSingleton<ServicioKeycloakUsuarios, KeycloakDePrueba>());
    }
}
