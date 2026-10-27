using Api.Servicios;

namespace Api.PruebasUnitarias;

public class ServicioEmbeddingsPruebas
{
    private static readonly string[] Variables = ["EMBEDDINGS_MODELO_RUTA", "EMBEDDINGS_TOKENIZADOR_RUTA"];

    private readonly Dictionary<string, string?> _valoresAnteriores = new();

    [SetUp]
    public void Preparar()
    {
        foreach (var variable in Variables)
        {
            _valoresAnteriores[variable] = Environment.GetEnvironmentVariable(variable);
        }
    }

    [TearDown]
    public void Limpiar()
    {
        foreach (var variable in Variables)
        {
            Environment.SetEnvironmentVariable(variable, _valoresAnteriores[variable]);
        }
    }

    [Test]
    public void Normalizar_DejaElVectorConLargoUno()
    {
        var vector = new float[ServicioEmbeddings.DimensionesDelEmbedding];
        vector[0] = 3;
        vector[1] = 4;

        var normalizado = ServicioEmbeddings.Normalizar(vector);

        Assert.That(normalizado[0], Is.EqualTo(0.6f).Within(1e-6));
        Assert.That(normalizado[1], Is.EqualTo(0.8f).Within(1e-6));
        Assert.That(normalizado.Sum(valor => valor * valor), Is.EqualTo(1f).Within(1e-5));
    }

    [Test]
    public void Normalizar_RechazaVectoresInvalidos()
    {
        var conInfinito = new float[ServicioEmbeddings.DimensionesDelEmbedding];
        conInfinito[0] = float.PositiveInfinity;

        Assert.Throws<InvalidDataException>(() => ServicioEmbeddings.Normalizar(new float[ServicioEmbeddings.DimensionesDelEmbedding]));
        Assert.Throws<InvalidDataException>(() => ServicioEmbeddings.Normalizar(new float[10]));
        Assert.Throws<InvalidDataException>(() => ServicioEmbeddings.Normalizar(conInfinito));
    }

    [Test]
    public void SinElModeloConfiguradoAvisaQueFaltaElModeloLocal()
    {
        Environment.SetEnvironmentVariable("EMBEDDINGS_MODELO_RUTA", null);
        Environment.SetEnvironmentVariable("EMBEDDINGS_TOKENIZADOR_RUTA", null);
        using var servicio = new ServicioEmbeddings();

        Assert.Throws<ModeloLocalNoDisponibleException>(() => servicio.Crear(["cambio de aceite"]));
    }

    [Test]
    public void ConUnaRutaInexistenteAvisaQueFaltaElModeloLocal()
    {
        Environment.SetEnvironmentVariable("EMBEDDINGS_MODELO_RUTA", Path.Combine(Path.GetTempPath(), "no-existe.onnx"));
        Environment.SetEnvironmentVariable("EMBEDDINGS_TOKENIZADOR_RUTA", Path.Combine(Path.GetTempPath(), "no-existe.json"));
        using var servicio = new ServicioEmbeddings();

        Assert.Throws<ModeloLocalNoDisponibleException>(() => servicio.Crear(["cambio de aceite"]));
    }
}
