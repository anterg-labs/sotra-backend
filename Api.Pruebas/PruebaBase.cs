using NUnit.Framework;

namespace Api.Pruebas;

public abstract class PruebaBase
{
    protected HttpClient Cliente { get; private set; } = null!;

    [SetUp]
    public void PrepararCliente()
    {
        Cliente = EntornoDePruebas.Fabrica.CreateClient();
        EntornoDePruebas.VerificarBaseDePruebas();
    }

    [TearDown]
    public void LiberarCliente()
    {
        Cliente.Dispose();
    }
}
