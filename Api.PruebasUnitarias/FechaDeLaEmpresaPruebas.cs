using Api.Infraestructura;

namespace Api.PruebasUnitarias;

public class FechaDeLaEmpresaPruebas
{
    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    [Test]
    public void DespuesDeLas21EnUruguayLaFechaSigueSiendoLaDelDiaLocalYNoLaDeUtc()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 29, 1, 0, 0, TimeSpan.Zero));

        Assert.That(FechaDeLaEmpresa.Hoy(reloj), Is.EqualTo(new DateOnly(2026, 10, 28)));
    }

    [Test]
    public void DeDiaLaFechaLocalCoincideConLaDeUtc()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 28, 15, 0, 0, TimeSpan.Zero));

        Assert.That(FechaDeLaEmpresa.Hoy(reloj), Is.EqualTo(new DateOnly(2026, 10, 28)));
    }
}
