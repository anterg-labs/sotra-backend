using Api.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Api.PruebasUnitarias;

public class ConfiguracionDeErroresPruebas
{
    [TestCase(true)]
    [TestCase(false)]
    public void LaValidacionConservaCodigoYMensajeSinDependerDelOrdenDeRegistro(bool registrarErroresPrimero)
    {
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        if (registrarErroresPrimero) servicios.AgregarManejoDeErrores();
        servicios.AddControllers();
        if (!registrarErroresPrimero) servicios.AgregarManejoDeErrores();

        using var proveedor = servicios.BuildServiceProvider();
        var opciones = proveedor.GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
        var estado = new ModelStateDictionary();
        estado.AddModelError("Nombre", "El campo Nombre es obligatorio.");
        var contexto = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor(), estado);
        var respuesta = opciones.InvalidModelStateResponseFactory(contexto);

        Assert.That(respuesta, Is.TypeOf<BadRequestObjectResult>());
        var detalles = ((BadRequestObjectResult)respuesta).Value as ValidationProblemDetails;
        Assert.That(detalles, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(detalles!.Extensions["codigo"], Is.EqualTo("VALIDACION_FALLIDA"));
            Assert.That(detalles.Extensions["mensaje"], Is.EqualTo("El campo Nombre es obligatorio."));
            Assert.That(detalles.Errors["Nombre"], Does.Contain("El campo Nombre es obligatorio."));
        });
    }
}
