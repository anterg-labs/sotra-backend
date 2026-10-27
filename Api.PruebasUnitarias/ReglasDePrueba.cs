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
        new(1, "creada", "Creada", 1, "pendiente", false, null, "neutro", "sin_asignar", true),
        new(3, "asignada", "Asignada", 2, "pendiente", true, null, "info", "demorada_para_iniciar", false),
        new(2, "en_ejecucion", "En ejecución", 3, "en_curso", true, "inicio", "info", null, false),
        new(4, "pausada", "Pausada", 4, "en_curso", false, null, "alerta", "pausada_demasiado", false),
        new(5, "finalizada", "Finalizada", 5, "por_validar", false, null, "destacado", null, false),
        new(6, "cerrada", "Cerrada", 6, "cerrada", false, "cierre", "exito", null, false),
        new(7, "cancelada", "Cancelada", 7, "cancelada", false, null, "neutro", null, false),
    ];

    public static readonly IReadOnlyList<TransicionDeEstado> TransicionesDocumentadas =
    [
        new("creada", "asignada", false, false, null, false, true),
        new("creada", "cancelada", true, false, null, false, false),
        new("asignada", "en_ejecucion", false, true, "Iniciar", true, false),
        new("asignada", "cancelada", true, false, null, false, false),
        new("en_ejecucion", "pausada", true, true, "Pausar", false, false),
        new("en_ejecucion", "finalizada", false, true, "Finalizar", true, false),
        new("pausada", "en_ejecucion", false, true, "Reanudar", true, false),
        new("pausada", "cancelada", true, false, null, false, false),
        new("finalizada", "cerrada", true, false, null, false, false),
        new("finalizada", "asignada", true, false, null, false, false),
    ];

    public static ReglasDeEstadoOrden Documentadas() => new(EstadosDocumentados, TransicionesDocumentadas);
}
