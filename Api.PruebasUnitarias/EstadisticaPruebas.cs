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
}
