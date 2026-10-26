using System.Reflection;
using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

/// <summary>
/// Red de seguridad de las reglas de estado de las órdenes: la matriz de la sección 13.1 de «Flujo de datos SOTRA.md»,
/// escrita aparte del código, contra las 49 combinaciones de estado actual y nuevo, con y sin técnico y con y sin motivo.
/// </summary>
public class MatrizDeEstadosPruebas
{
    private static readonly string[] Estados = ["creada", "asignada", "en_ejecucion", "pausada", "finalizada", "cerrada", "cancelada"];

    // Sección 13.1: «Estado actual → puede pasar a».
    private static readonly Dictionary<string, string[]> Permitidas = new()
    {
        ["creada"] = ["asignada", "cancelada"],
        ["asignada"] = ["en_ejecucion", "cancelada"],
        ["en_ejecucion"] = ["pausada", "finalizada"],
        ["pausada"] = ["en_ejecucion", "cancelada"],
        ["finalizada"] = ["cerrada", "asignada"],
        ["cerrada"] = [],
        ["cancelada"] = [],
    };

    // «asignada y en_ejecucion exigen técnico asignado».
    private static readonly string[] ExigenTecnico = ["asignada", "en_ejecucion"];

    // «El motivo es obligatorio al pasar a pausada, cancelada o cerrada, y al reasignar una orden finalizada».
    private static bool ExigeMotivo(string actual, string nuevo) =>
        nuevo is "pausada" or "cancelada" or "cerrada" || (actual == "finalizada" && nuevo == "asignada");

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

    private static string? Obtenido(string actual, string nuevo, bool conTecnico, bool conMotivo)
    {
        var metodo = typeof(OrdenTrabajoDAO).GetMethod("ValidarTransicion", BindingFlags.NonPublic | BindingFlags.Static)!;
        try
        {
            metodo.Invoke(null, [actual, nuevo, conTecnico ? 7L : null, conMotivo ? "motivo" : null]);
            return null;
        }
        catch (TargetInvocationException excepcion) when (excepcion.InnerException is InvalidOperationException error)
        {
            return error.Message;
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
    public void LaMatrizCubreLosSieteEstadosYSusCuarentaYNueveParejas()
    {
        Assert.That(Combinaciones().Count(), Is.EqualTo(7 * 7 * 4));
        Assert.That(OrdenTrabajoDAO.EstadosDelCiclo, Is.EquivalentTo(Estados));
    }
}
