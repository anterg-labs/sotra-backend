using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Pruebas;

public class FabricaApi : WebApplicationFactory<PuntoDeEntrada>
{
    protected override void ConfigureWebHost(IWebHostBuilder constructor)
    {
        constructor.UseEnvironment("Development");
    }
}
