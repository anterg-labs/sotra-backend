using Api.Servicios;
using NUnit.Framework;

namespace Api.PruebasUnitarias;

public class EstadisticaPruebas
{
    [Test]
    public void LaPendienteDeUnaSerieQueSeAcortaEsNegativa()
    {
        Assert.That(Estadistica.Pendiente([100, 80, 60, 40]), Is.EqualTo(-20).Within(1e-9));
    }

    [Test]
    public void LaPendienteDeUnaSerieConstanteEsCeroYConUnSoloValorTambien()
    {
        Assert.That(Estadistica.Pendiente([30, 30, 30]), Is.EqualTo(0).Within(1e-9));
        Assert.That(Estadistica.Pendiente([30]), Is.EqualTo(0));
        Assert.That(Estadistica.Pendiente([]), Is.EqualTo(0));
    }

    [TestCase(0.6)]
    [TestCase(1.0)]
    [TestCase(2.5)]
    public void ElAjusteDeWeibullRecuperaLaFormaYLaEscalaDeUnaMuestraExacta(double forma)
    {
        const double escala = 120;
        const int n = 40;
        var tiempos = Enumerable.Range(1, n)
            .Select(i => escala * Math.Pow(-Math.Log(1 - (i - 0.3) / (n + 0.4)), 1 / forma))
            .ToList();

        var ajuste = Estadistica.AjusteWeibull(tiempos);

        Assert.That(ajuste, Is.Not.Null);
        Assert.That(ajuste!.Value.Forma, Is.EqualTo(forma).Within(1e-6));
        Assert.That(ajuste.Value.Escala, Is.EqualTo(escala).Within(1e-4));
        Assert.That(ajuste.Value.R2, Is.EqualTo(1).Within(1e-9));
    }

    [Test]
    public void ElAjusteDeWeibullNoSeCalculaConPocosDatosNiConTiemposIguales()
    {
        Assert.That(Estadistica.AjusteWeibull([10, 20]), Is.Null);
        Assert.That(Estadistica.AjusteWeibull([10, 10, 10, 10]), Is.Null);
        Assert.That(Estadistica.AjusteWeibull([0, -1, 0]), Is.Null);
    }
}
