using Api.Servicios;
using NUnit.Framework;

namespace Api.PruebasUnitarias;

/// <summary>El corpus de <c>herramientas-plan/ia/corpus</c> debe poder leerse con el extractor real de PDF.</summary>
public class CorpusDeEvaluacionIaPruebas
{
    // El corpus vive fuera del repositorio del backend: sin él (por ejemplo en CI) las pruebas se omiten.
    private static string RutaDelCorpus
    {
        get
        {
            var carpeta = new DirectoryInfo(AppContext.BaseDirectory);
            while (carpeta is not null && !Directory.Exists(Path.Combine(carpeta.FullName, "herramientas-plan")))
            {
                carpeta = carpeta.Parent;
            }

            if (carpeta is null)
            {
                Assert.Ignore("No se encontró herramientas-plan: el corpus de evaluación no está disponible.");
            }

            return Path.Combine(carpeta!.FullName, "herramientas-plan", "ia", "corpus");
        }
    }

    [Test]
    public void ElManualDeEvaluacionSeExtraeConSusValoresClave()
    {
        var fragmentos = DocumentoPdf.Extraer(File.ReadAllBytes(Path.Combine(RutaDelCorpus, "manual-retroexcavadora-rx200.pdf")));
        var texto = string.Join(" ", fragmentos.Select(f => f.Texto));

        Assert.That(fragmentos, Is.Not.Empty);
        Assert.That(texto, Does.Contain("25 Nm").And.Contain("ISO VG 46").And.Contain("15W-40").And.Contain("250 horas"));
    }

    [Test]
    public void LaGuiaDeSeguridadSeExtrae()
    {
        var fragmentos = DocumentoPdf.Extraer(File.ReadAllBytes(Path.Combine(RutaDelCorpus, "guia-seguridad-taller.pdf")));

        Assert.That(string.Join(" ", fragmentos.Select(f => f.Texto)), Does.Contain("bloquee y etiquete"));
    }
}
