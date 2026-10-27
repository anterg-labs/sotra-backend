using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

/// <summary>
/// Red de seguridad de las reglas de estado de las órdenes: la matriz de la sección 13.1 de «Flujo de datos SOTRA.md»,
/// escrita aparte del código, contra las 36 combinaciones de estado actual y nuevo, con y sin técnico y con y sin motivo.
/// </summary>
public class MatrizDeEstadosPruebas
{
    private static readonly string[] Estados = ["pendiente", "en_progreso", "en_revision", "cerrado", "pausado", "eliminado"];

    // Sección 13.1: «Estado actual → puede pasar a».
    private static readonly Dictionary<string, string[]> Permitidas = new()
    {
        ["pendiente"] = ["en_progreso", "eliminado"],
        ["en_progreso"] = ["en_revision", "pausado"],
        ["pausado"] = ["en_progreso", "eliminado"],
        ["en_revision"] = ["cerrado", "pendiente"],
        ["cerrado"] = [],
        ["eliminado"] = [],
    };

    // «en_progreso exige técnico asignado».
    private static readonly string[] ExigenTecnico = ["en_progreso"];

    // «El motivo es obligatorio al pasar a pausado o eliminado, y al devolver una orden de en_revision a pendiente».
    private static bool ExigeMotivo(string actual, string nuevo) =>
        nuevo is "pausado" or "eliminado" || (actual == "en_revision" && nuevo == "pendiente");

    private static string? Esperado(string actual, string nuevo, bool conTecnico, bool conMotivo)
    {
        if (actual == nuevo)
        {
            return null;
        }

        if (!Permitidas[actual].Contains(nuevo))
        {
            return "TRANSICION_INVALIDA";
        }

        if (ExigenTecnico.Contains(nuevo) && !conTecnico)
        {
            return "TECNICO_OBLIGATORIO";
        }

        if (ExigeMotivo(actual, nuevo) && !conMotivo)
        {
            return "MOTIVO_OBLIGATORIO";
        }

        return null;
    }

    private static readonly ReglasDeEstadoOrden Reglas = ReglasDePrueba.Documentadas();

    private static string? Obtenido(string actual, string nuevo, bool conTecnico, bool conMotivo)
    {
        try
        {
            Reglas.ValidarTransicion(actual, nuevo, conTecnico ? 7L : null, conMotivo ? "motivo" : null);
            return null;
        }
        catch (ErrorDeOrdenException error)
        {
            return error.Codigo;
        }
    }

    public static IEnumerable<TestCaseData> Combinaciones()
    {
        foreach (var actual in Estados)
        {
            foreach (var nuevo in Estados)
            {
                foreach (var conTecnico in new[] { false, true })
                {
                    foreach (var conMotivo in new[] { false, true })
                    {
                        yield return new TestCaseData(actual, nuevo, conTecnico, conMotivo)
                            .SetName($"{actual}_a_{nuevo}_{(conTecnico ? "con" : "sin")}Tecnico_{(conMotivo ? "con" : "sin")}Motivo");
                    }
                }
            }
        }
    }

    [TestCaseSource(nameof(Combinaciones))]
    public void CadaCombinacionSeResuelveComoLoDocumenta(string actual, string nuevo, bool conTecnico, bool conMotivo)
    {
        Assert.That(Obtenido(actual, nuevo, conTecnico, conMotivo), Is.EqualTo(Esperado(actual, nuevo, conTecnico, conMotivo)));
    }

    [Test]
    public void LaMatrizCubreLosSeisEstadosYSusTreintaYSeisParejas()
    {
        Assert.That(Combinaciones().Count(), Is.EqualTo(6 * 6 * 4));
        Assert.That(Reglas.Estados.Select(estado => estado.Nombre), Is.EquivalentTo(Estados));
    }
}
