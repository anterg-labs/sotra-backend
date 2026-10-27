namespace Api.DTOs.Reportes;

/// <summary>Cumplimiento de la fecha prevista de un grupo de órdenes cerradas.</summary>
public class CumplimientoGrupoDto
{
    /// <summary>Identificador del tipo de máquina; vacío cuando el grupo es un tipo de orden.</summary>
    public long? Id { get; init; }
    /// <summary>Nombre del grupo (`preventivo`, `correctivo` o el tipo de máquina).</summary>
    public required string Nombre { get; init; }
    /// <summary>Órdenes cerradas evaluadas.</summary>
    public int Evaluadas { get; init; }
    /// <summary>Órdenes cerradas a más tardar en su fecha prevista.</summary>
    public int EnPlazo { get; init; }
    /// <summary>Porcentaje en plazo; vacío si no hay órdenes evaluadas.</summary>
    public double? PorcentajeEnPlazo { get; init; }
}

/// <summary>Pausas de un motivo, con el tiempo que sumaron.</summary>
public class PausaPorMotivoDto
{
    /// <summary>Código estable del motivo o `sin_codificar`.</summary>
    public required string Codigo { get; init; }
    /// <summary>Texto del motivo.</summary>
    public required string Motivo { get; init; }
    /// <summary>Pausas con ese motivo.</summary>
    public int Cantidad { get; init; }
    /// <summary>Horas totales en pausa por ese motivo (hasta el cambio siguiente de cada orden).</summary>
    public double Horas { get; init; }
    /// <summary>Horas promedio por pausa.</summary>
    public double? HorasPromedio { get; init; }
}

/// <summary>Comparación entre el estimado y el real de las órdenes que tienen tiempo estimado.</summary>
public class EstimacionDto
{
    /// <summary>Órdenes con estimado y ejecución terminada.</summary>
    public int Ordenes { get; init; }
    /// <summary>Horas estimadas promedio.</summary>
    public double? HorasEstimadas { get; init; }
    /// <summary>Horas reales promedio (del inicio a la primera revisión).</summary>
    public double? HorasReales { get; init; }
    /// <summary>Desviación promedio del real frente al estimado, en porcentaje (positiva si tardó más).</summary>
    public double? DesviacionPromedioPorcentaje { get; init; }
}

public class RangoDeFechasDto
{
    /// <summary>Primer día (`aaaa-mm-dd`).</summary>
    public required string Desde { get; init; }
    /// <summary>Último día (`aaaa-mm-dd`).</summary>
    public required string Hasta { get; init; }
}

public class PuntoDeSerieDto
{
    /// <summary>Mes de la serie (`aaaa-mm-01`).</summary>
    public required string Periodo { get; init; }
    /// <summary>Valor del indicador en ese mes; vacío si no hay datos.</summary>
    public double? Valor { get; init; }
}

/// <summary>Un indicador del período, con el período anterior, el mismo período del año pasado y la serie mensual.</summary>
public class MetricaComparadaDto
{
    /// <summary>Código estable: `ordenes_creadas`, `preventivas_porcentaje`, `planificado_horas_porcentaje`, `cumplimiento_preventivo`, `tiempo_reparacion_horas` u `horas_parada`.</summary>
    public required string Codigo { get; init; }
    /// <summary>Nombre para mostrar.</summary>
    public required string Nombre { get; init; }
    /// <summary>Unidad: `ordenes`, `porcentaje` u `horas`.</summary>
    public required string Unidad { get; init; }
    /// <summary>Valor del período; vacío si no hay datos.</summary>
    public double? Actual { get; init; }
    /// <summary>Valor del período anterior de igual duración.</summary>
    public double? Anterior { get; init; }
    /// <summary>Valor del mismo período un año antes.</summary>
    public double? AnioAnterior { get; init; }
    /// <summary>Variación porcentual frente al período anterior; vacía si falta alguno de los dos valores o el anterior es cero.</summary>
    public double? VariacionPorcentual { get; init; }
    /// <summary>`true` si subir es mejor, `false` si bajar es mejor, vacío si no hay juicio de valor.</summary>
    public bool? MejorSiSube { get; init; }
    /// <summary>Cantidad de órdenes que respaldan el valor actual.</summary>
    public int Muestra { get; init; }
    /// <summary>Valores mensuales de los últimos seis meses.</summary>
    public required List<PuntoDeSerieDto> Serie { get; init; }
}

public class ComparativaDto
{
    /// <summary>Período analizado.</summary>
    public required RangoDeFechasDto Periodo { get; init; }
    /// <summary>Período anterior de igual duración.</summary>
    public required RangoDeFechasDto PeriodoAnterior { get; init; }
    /// <summary>Mismo período un año antes.</summary>
    public required RangoDeFechasDto PeriodoAnioAnterior { get; init; }
    /// <summary>Indicadores comparados.</summary>
    public required List<MetricaComparadaDto> Metricas { get; init; }
}

public class OrdenAntiguaDto
{
    /// <summary>Identificador de la orden.</summary>
    public long OrdenId { get; init; }
    /// <summary>Nombre del activo.</summary>
    public required string NombreActivo { get; init; }
    /// <summary>Cliente.</summary>
    public required string Cliente { get; init; }
    /// <summary>Estado actual.</summary>
    public required string Estado { get; init; }
    /// <summary>Días desde la creación.</summary>
    public double EdadDias { get; init; }
}

public class RangoDeEdadDto
{
    /// <summary>Etiqueta del rango (`hasta_3`, `4_a_7`, `8_a_30`, `31_a_90`, `mas_de_90`).</summary>
    public required string Rango { get; init; }
    /// <summary>Texto para mostrar.</summary>
    public required string Etiqueta { get; init; }
    /// <summary>Órdenes abiertas en ese rango de antigüedad.</summary>
    public int Cantidad { get; init; }
}

public class EnvejecimientoDto
{
    /// <summary>Órdenes abiertas (pendientes, en curso o en revisión).</summary>
    public int Abiertas { get; init; }
    /// <summary>Órdenes abiertas por rango de antigüedad desde su creación.</summary>
    public required List<RangoDeEdadDto> Rangos { get; init; }
    /// <summary>Órdenes cerradas por semana en las últimas ocho semanas.</summary>
    public double CierresPorSemana { get; init; }
    /// <summary>Semanas que llevaría vaciar lo abierto al ritmo de cierre actual; vacío si no hubo cierres.</summary>
    public double? BacklogEnSemanas { get; init; }
    /// <summary>Las órdenes abiertas más antiguas.</summary>
    public required List<OrdenAntiguaDto> MasAntiguas { get; init; }
}

public class OrdenAtipicaDto
{
    /// <summary>Identificador de la orden.</summary>
    public long OrdenId { get; init; }
    /// <summary>Nombre del activo.</summary>
    public required string NombreActivo { get; init; }
    /// <summary>Tipo de máquina del grupo de comparación.</summary>
    public required string TipoMaquina { get; init; }
    /// <summary>Horas de la creación al cierre.</summary>
    public double Horas { get; init; }
    /// <summary>Mediana del tipo de máquina.</summary>
    public double MedianaHoras { get; init; }
    /// <summary>Límite superior normal del tipo de máquina (tercer cuartil más una vez y media el rango intercuartílico).</summary>
    public double LimiteHoras { get; init; }
    /// <summary>Cuántas veces supera la mediana.</summary>
    public double VecesLaMediana { get; init; }
}

public class AtipicasDto
{
    /// <summary>Mínimo de órdenes cerradas por tipo de máquina para evaluar un grupo.</summary>
    public int MinimoPorGrupo { get; init; }
    /// <summary>Órdenes correctivas cerradas con duración muy por encima de lo habitual para su tipo de máquina.</summary>
    public required List<OrdenAtipicaDto> Elementos { get; init; }
}

public class DesgasteActivoDto
{
    /// <summary>Identificador del activo.</summary>
    public long ActivoId { get; init; }
    /// <summary>Nombre del activo.</summary>
    public required string NombreActivo { get; init; }
    /// <summary>Cliente.</summary>
    public required string Cliente { get; init; }
    /// <summary>Modelo.</summary>
    public required string Modelo { get; init; }
    /// <summary>Intervalos entre correctivas consecutivas, en días, del más antiguo al más reciente.</summary>
    public required List<double> IntervalosDias { get; init; }
    /// <summary>Días promedio entre correctivas.</summary>
    public double PromedioDias { get; init; }
    /// <summary>Cambio de cada intervalo respecto del anterior, en días (negativo si los intervalos se acortan).</summary>
    public double PendienteDiasPorIntervalo { get; init; }
    /// <summary>`true` si los intervalos se acortan de forma sostenida.</summary>
    public bool SeAcortan { get; init; }
    /// <summary>Unidad de uso del activo (`horas` o `kilometros`).</summary>
    public required string UnidadUso { get; init; }
    /// <summary>Uso promedio entre correctivas, si hay lecturas; vacío si no.</summary>
    public double? UsoPromedioEntreFallas { get; init; }
}

public class PatronDeFallasDto
{
    /// <summary>Identificador del modelo.</summary>
    public long ModeloId { get; init; }
    /// <summary>Nombre del modelo.</summary>
    public required string Modelo { get; init; }
    /// <summary>Intervalos usados para la estimación.</summary>
    public int Intervalos { get; init; }
    /// <summary>Forma de la distribución de Weibull (menor que 1: fallas tempranas; cerca de 1: aleatorias; mayor que 1: desgaste).</summary>
    public double Forma { get; init; }
    /// <summary>Escala característica, en días.</summary>
    public double EscalaDias { get; init; }
    /// <summary>Calidad del ajuste (R²).</summary>
    public double AjusteR2 { get; init; }
    /// <summary>`tempranas`, `aleatorias` o `desgaste`.</summary>
    public required string Patron { get; init; }
}

public class DesgasteDto
{
    /// <summary>Mínimo de correctivas de un activo para analizar sus intervalos.</summary>
    public int MinimoDeIntervalos { get; init; }
    /// <summary>Mínimo de intervalos de un modelo para estimar su patrón de fallas.</summary>
    public int MinimoParaPatron { get; init; }
    /// <summary>Activos con datos suficientes, los que más acortan sus intervalos primero.</summary>
    public required List<DesgasteActivoDto> Activos { get; init; }
    /// <summary>Patrón de fallas por modelo; vacío si ninguno alcanza el mínimo de datos.</summary>
    public required List<PatronDeFallasDto> Patrones { get; init; }
    /// <summary>`true` si no hubo datos suficientes para ningún análisis.</summary>
    public bool DatosInsuficientes { get; init; }
}

public class EvidenciaDeAvisoDto
{
    /// <summary>Valor observado (porcentaje, cantidad, horas o días según la regla).</summary>
    public double Valor { get; init; }
    /// <summary>Umbral superado.</summary>
    public double Umbral { get; init; }
    /// <summary>Texto de la unidad del valor (`%`, `correctivas`, `órdenes`, `días`, `horas`).</summary>
    public required string Unidad { get; init; }
    /// <summary>Ventana en días que se analizó, si corresponde.</summary>
    public int? VentanaDias { get; init; }
    /// <summary>Cantidad de órdenes o eventos que respaldan el valor.</summary>
    public int Muestra { get; init; }
}

public class AvisoDto
{
    /// <summary>Código de la regla (`R1`, `R2`...).</summary>
    public required string Regla { get; init; }
    /// <summary>`atencion` o `critico`.</summary>
    public required string Severidad { get; init; }
    /// <summary>Título corto que dice la conclusión.</summary>
    public required string Titulo { get; init; }
    /// <summary>Explicación de cómo se calculó.</summary>
    public required string Detalle { get; init; }
    /// <summary>Cifras exactas de la regla.</summary>
    public required EvidenciaDeAvisoDto Evidencia { get; init; }
    /// <summary>Identificador del activo, si el aviso es de una máquina.</summary>
    public long? ActivoId { get; init; }
    /// <summary>Estado de las órdenes para abrir el listado filtrado, si corresponde.</summary>
    public string? EnlaceEstado { get; init; }
}

public class AvisosDto
{
    /// <summary>Cantidad de avisos.</summary>
    public int Total { get; init; }
    /// <summary>Avisos críticos.</summary>
    public int Criticos { get; init; }
    /// <summary>Avisos activos, los críticos primero.</summary>
    public required List<AvisoDto> Avisos { get; init; }
}

public class ReglaAvisoDto
{
    /// <summary>Código de la regla.</summary>
    public required string Codigo { get; init; }
    /// <summary>Nombre de la regla.</summary>
    public required string Nombre { get; init; }
    /// <summary>`true` si la regla está encendida.</summary>
    public bool Activa { get; init; }
    /// <summary>Umbral de atención.</summary>
    public decimal Umbral { get; init; }
    /// <summary>Umbral crítico, si la regla tiene.</summary>
    public decimal? UmbralCritico { get; init; }
    /// <summary>Ventana de análisis en días, si corresponde.</summary>
    public int? VentanaDias { get; init; }
    /// <summary>Ventana del nivel crítico en días, si corresponde.</summary>
    public int? VentanaCriticaDias { get; init; }
    /// <summary>Mínimo de datos para que la regla se evalúe.</summary>
    public int MinimoDatos { get; init; }
}

public class ReglaAvisoActualizarDto
{
    /// <summary>Enciende o apaga la regla.</summary>
    public bool Activa { get; init; }
    /// <summary>Umbral de atención (cualquier número válido; sin mínimos ni máximos fijos).</summary>
    public decimal Umbral { get; init; }
    /// <summary>Umbral crítico, opcional.</summary>
    public decimal? UmbralCritico { get; init; }
    /// <summary>Ventana de análisis en días, opcional (mayor que cero).</summary>
    public int? VentanaDias { get; init; }
    /// <summary>Ventana del nivel crítico en días, opcional (mayor que cero).</summary>
    public int? VentanaCriticaDias { get; init; }
    /// <summary>Mínimo de datos (cero o más).</summary>
    public int MinimoDatos { get; init; }
}
