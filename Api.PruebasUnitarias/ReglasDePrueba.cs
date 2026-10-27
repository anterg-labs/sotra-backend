using SOTRA_BACKEND.Operaciones;

namespace Api.PruebasUnitarias;

/// <summary>
/// Las reglas de los estados de las órdenes tal como las documenta la sección 13.1 de «Flujo de datos SOTRA.md».
/// Es la referencia de las pruebas: las unitarias comprueban el motor contra ella y la de integración comprueba que la
/// base tenga exactamente esto. Este archivo lo compilan los dos proyectos de pruebas.
/// </summary>
public static class ReglasDePrueba
{
    public static readonly IReadOnlyList<EstadoDeOrden> EstadosDocumentados =
    [
        new(1, "pendiente", "Pendiente", 1, "pendiente", false, null, "neutro", "demorada_para_iniciar", true),
        new(2, "en_progreso", "En progreso", 2, "en_curso", true, "inicio", "info", null, false),
        new(5, "en_revision", "En revisión", 3, "por_validar", false, null, "destacado", null, false),
        new(6, "cerrado", "Cerrado", 4, "cerrada", false, "cierre", "exito", null, false),
        new(4, "pausado", "Pausado", 5, "en_curso", false, null, "alerta", "pausada_demasiado", false),
        new(7, "eliminado", "Eliminado", 6, "eliminada", false, null, "neutro", null, false),
    ];

    public static readonly IReadOnlyList<TransicionDeEstado> TransicionesDocumentadas =
    [
        new("pendiente", "en_progreso", false, true, "Iniciar", true, false),
        new("pendiente", "eliminado", true, false, null, false, false),
        new("en_progreso", "en_revision", false, true, "Enviar a revisión", true, false),
        new("en_progreso", "pausado", true, true, "Pausar", false, false),
        new("pausado", "en_progreso", false, true, "Reanudar", true, false),
        new("pausado", "eliminado", true, false, null, false, false),
        new("en_revision", "cerrado", false, false, null, false, false),
        new("en_revision", "pendiente", true, false, null, false, false),
    ];

    public static ReglasDeEstadoOrden Documentadas() => new(EstadosDocumentados, TransicionesDocumentadas);
}
