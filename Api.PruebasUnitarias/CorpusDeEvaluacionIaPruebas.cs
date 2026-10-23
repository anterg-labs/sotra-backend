using Api.Servicios;
using NUnit.Framework;

namespace Api.PruebasUnitarias;

/// <summary>El corpus de <c>sotra-test-automation/herramientas-previas/ia/corpus</c> debe poder leerse con el extractor real de PDF.</summary>
public class CorpusDeEvaluacionIaPruebas
{
    private static readonly string[] RutaRelativa = ["sotra-test-automation", "herramientas-previas", "ia", "corpus"];

    /// <summary>Busca el corpus en el repositorio hermano; si no está (por ejemplo, en la integración continua del backend), omite la prueba.</summary>
    private static string RutaDelCorpus()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta is not null; carpeta = carpeta.Parent)
        {
            var candidata = Path.Combine([carpeta.FullName, .. RutaRelativa]);
            if (Directory.Exists(candidata))
            {
                return candidata;
            }
        }

        Assert.Ignore("El corpus de evaluación vive en sotra-test-automation/herramientas-previas/ia/corpus y no está disponible junto a este repositorio.");
        return string.Empty;
    }

    [Test]
    public void ElManualDeEvaluacionSeExtraeConSusValoresClave()
    {
        var fragmentos = DocumentoPdf.Extraer(File.ReadAllBytes(Path.Combine(RutaDelCorpus(), "manual-retroexcavadora-rx200.pdf")));
        var texto = string.Join(" ", fragmentos.Select(f => f.Texto));

        Assert.That(fragmentos, Is.Not.Empty);
        Assert.That(texto, Does.Contain("25 Nm").And.Contain("ISO VG 46").And.Contain("15W-40").And.Contain("250 horas"));
    }

    [Test]
    public void LaGuiaDeSeguridadSeExtrae()
    {
        var fragmentos = DocumentoPdf.Extraer(File.ReadAllBytes(Path.Combine(RutaDelCorpus(), "guia-seguridad-taller.pdf")));

        Assert.That(string.Join(" ", fragmentos.Select(f => f.Texto)), Does.Contain("bloquee y etiquete"));
    }
}
