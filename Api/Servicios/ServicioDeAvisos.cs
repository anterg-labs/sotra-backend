using System.Globalization;
using Api.DTOs.Reportes;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;
using SOTRA_BACKEND.Operaciones;

namespace Api.Servicios;

/// <summary>
/// Evalúa las reglas de ineficiencia del mantenimiento: cálculos sencillos sobre órdenes, maquinaria y repuestos, sin
/// inteligencia artificial y sin referirse nunca a personas. Los umbrales salen de <c>operativo.regla_aviso</c>.
/// </summary>
public class ServicioDeAvisos(AnaliticaAvanzadaDAO dao, SotraContext contexto)
{
    private const string Atencion = "atencion";
    private const string Critico = "critico";
    private const int MaximoDeAvisosPorRegla = 15;

    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-UY");

    public async Task<AvisosDto> EvaluarAsync(CancellationToken cancelacion)
    {
        var reglas = await contexto.ReglasAviso.AsNoTracking().Where(regla => regla.Activa).ToDictionaryAsync(regla => regla.Codigo, cancelacion);
        var avisos = new List<AvisoDto>();

        if (reglas.TryGetValue("R1", out var r1)) avisos.AddRange(await MaquinaQueReincideAsync(r1, cancelacion));
        if (reglas.TryGetValue("R2", out var r2)) avisos.AddRange(await TrabajoCorrectivoAltoAsync(r2, cancelacion));
        if (reglas.TryGetValue("R3", out var r3)) avisos.AddRange(await PreventivosIncumplidosAsync(r3, cancelacion));
        if (reglas.TryGetValue("R7", out var r7)) avisos.AddRange(await OrdenesEnvejecidasAsync(r7, cancelacion));
        if (reglas.TryGetValue("R8", out var r8)) avisos.AddRange(await RevisionEstancadaAsync(r8, cancelacion));
        if (reglas.TryGetValue("R12", out var r12)) avisos.AddRange(await ReparacionesMasLentasAsync(r12, cancelacion));
        if (reglas.TryGetValue("R13", out var r13)) avisos.AddRange(await ParadaConcentradaAsync(r13, cancelacion));
        if (reglas.TryGetValue("R17", out var r17)) avisos.AddRange(await CriticasSinAsignarAsync(r17, cancelacion));
        if (reglas.TryGetValue("R18", out var r18)) avisos.AddRange(await OrdenesSinMovimientoAsync(r18, cancelacion));

        var ordenados = avisos
            .OrderByDescending(aviso => aviso.Severidad == Critico)
            .ThenBy(aviso => aviso.Regla.Length)
            .ThenBy(aviso => aviso.Regla, StringComparer.Ordinal)
            .ToList();
        return new AvisosDto { Total = ordenados.Count, Criticos = ordenados.Count(aviso => aviso.Severidad == Critico), Avisos = ordenados };
    }

    /// <summary>R1: una máquina acumula correctivas en una ventana corta (atención) o larga (crítico).</summary>
    private async Task<List<AvisoDto>> MaquinaQueReincideAsync(ReglaAviso regla, CancellationToken cancelacion)
    {
        var ventana = regla.VentanaDias ?? 180;
        var ventanaCritica = regla.VentanaCriticaDias ?? 730;
        var filas = await dao.ContarCorrectivasPorActivoAsync(ventana, ventanaCritica, cancelacion);
        var avisos = new List<AvisoDto>();
        foreach (var fila in filas)
        {
            var esCritico = regla.UmbralCritico is { } critico && fila.EnVentanaCritica >= (double)critico;
            if (!esCritico && fila.EnVentana < (double)regla.Umbral)
            {
                continue;
            }

            var cantidad = esCritico ? fila.EnVentanaCritica : fila.EnVentana;
            var dias = esCritico ? ventanaCritica : ventana;
            var umbral = esCritico ? (double)regla.UmbralCritico!.Value : (double)regla.Umbral;
            avisos.Add(Crear(regla, esCritico ? Critico : Atencion,
                $"{fila.NombreActivo} tuvo {cantidad} correctivas en {Meses(dias)}",
                $"Máquina que reincide ({fila.Cliente}): {cantidad} órdenes correctivas en los últimos {dias} días; el umbral es {umbral:0.##}.",
                cantidad, umbral, "correctivas", dias, cantidad, fila.ActivoId));
            if (avisos.Count >= MaximoDeAvisosPorRegla)
            {
                break;
            }
        }

        return avisos;
    }

    /// <summary>R2: el trabajo correctivo supera un porcentaje del total en un tipo de máquina.</summary>
    private async Task<List<AvisoDto>> TrabajoCorrectivoAltoAsync(ReglaAviso regla, CancellationToken cancelacion)
    {
        var ventana = regla.VentanaDias ?? 90;
        var avisos = new List<AvisoDto>();
        foreach (var fila in await dao.ObtenerProporcionDeCorrectivasPorTipoAsync(ventana, cancelacion))
        {
            if (fila.Total < regla.MinimoDatos || fila.Total == 0)
            {
                continue;
            }

            var porcentaje = Math.Round(fila.Parte * 100.0 / fila.Total, 1);
            if (porcentaje < (double)regla.Umbral)
            {
                continue;
            }

            var esCritico = regla.UmbralCritico is { } critico && porcentaje >= (double)critico;
            avisos.Add(Crear(regla, esCritico ? Critico : Atencion,
                $"El {Formatear(porcentaje)}% del trabajo en {fila.Nombre} es correctivo",
                $"{fila.Parte} de {fila.Total} órdenes de los últimos {ventana} días fueron correctivas; el umbral es {Formatear((double)regla.Umbral)}%.",
                porcentaje, (double)(esCritico ? regla.UmbralCritico!.Value : regla.Umbral), "%", ventana, fila.Total));
        }

        return avisos;
    }

    /// <summary>R3: el cumplimiento del plan preventivo cae por debajo de un umbral.</summary>
    private async Task<List<AvisoDto>> PreventivosIncumplidosAsync(ReglaAviso regla, CancellationToken cancelacion)
    {
        var ventana = regla.VentanaDias ?? 90;
        var fila = await dao.ObtenerPreventivosEnPlazoAsync(ventana, cancelacion);
        if (fila.Total == 0 || fila.Total < regla.MinimoDatos)
        {
            return [];
        }

        var porcentaje = Math.Round(fila.Parte * 100.0 / fila.Total, 1);
        if (porcentaje >= (double)regla.Umbral)
        {
            return [];
        }

        var esCritico = regla.UmbralCritico is { } critico && porcentaje < (double)critico;
        return [Crear(regla, esCritico ? Critico : Atencion,
            $"Solo el {Formatear(porcentaje)}% de los preventivos se cerró en fecha",
            $"{fila.Parte} de {fila.Total} preventivos cerrados en los últimos {ventana} días se cerraron a más tardar en su fecha prevista; se espera al menos {Formatear((double)regla.Umbral)}%.",
            porcentaje, (double)(esCritico ? regla.UmbralCritico!.Value : regla.Umbral), "%", ventana, fila.Total)];
    }

    /// <summary>R7: hay órdenes abiertas hace más de N días.</summary>
    private async Task<List<AvisoDto>> OrdenesEnvejecidasAsync(ReglaAviso regla, CancellationToken cancelacion)
    {
        var dias = (int)regla.Umbral;
        var diasCritico = regla.UmbralCritico is { } critico ? (int)critico : int.MaxValue;
        var fila = await dao.ContarAbiertasMasViejasQueAsync(dias, diasCritico, cancelacion);
        if (fila.Cantidad == 0 || fila.Cantidad < regla.MinimoDatos)
        {
            return [];
        }

        var esCritico = fila.Criticas > 0;
        return [Crear(regla, esCritico ? Critico : Atencion,
            $"{fila.Cantidad} órdenes llevan más de {dias} días abiertas",
            esCritico
                ? $"{fila.Criticas} de ellas superan los {diasCritico} días desde su creación."
                : $"Órdenes pendientes, en curso o en revisión con más de {dias} días desde su creación.",
            fila.Cantidad, dias, "órdenes", null, fila.Cantidad)];
    }

    /// <summary>R8: hay órdenes esperando validación en revisión hace más de N días.</summary>
    private async Task<List<AvisoDto>> RevisionEstancadaAsync(ReglaAviso regla, CancellationToken cancelacion)
    {
        var dias = (int)regla.Umbral;
        var diasCritico = regla.UmbralCritico is { } critico ? (int)critico : int.MaxValue;
        var fila = await dao.ContarSinMovimientoAsync("por_validar", dias, diasCritico, cancelacion);
        if (fila.Cantidad == 0 || fila.Cantidad < regla.MinimoDatos)
        {
            return [];
        }

        return [Crear(regla, fila.Criticas > 0 ? Critico : Atencion,
            $"{fila.Cantidad} órdenes esperan validación hace más de {dias} días",
            $"Órdenes en revisión sin ningún cambio de estado desde hace más de {dias} días.",
            fila.Cantidad, dias, "órdenes", null, fila.Cantidad, null, "en_revision")];
    }

    /// <summary>R12: las reparaciones de un tipo de máquina tardan más que su propio promedio histórico.</summary>
    private async Task<List<AvisoDto>> ReparacionesMasLentasAsync(ReglaAviso regla, CancellationToken cancelacion)
    {
        var ventana = regla.VentanaDias ?? 90;
        var avisos = new List<AvisoDto>();
        foreach (var fila in await dao.ObtenerTiempoDeReparacionPorTipoAsync(ventana, cancelacion))
        {
            if (fila.MuestraReciente < regla.MinimoDatos || fila.MuestraHistorica < regla.MinimoDatos
                || fila.PromedioReciente is not { } reciente || fila.PromedioHistorico is not { } historico || historico <= 0)
            {
                continue;
            }

            var aumento = Math.Round((reciente / historico - 1) * 100, 1);
            if (aumento < (double)regla.Umbral)
            {
                continue;
            }

            var esCritico = regla.UmbralCritico is { } critico && aumento >= (double)critico;
            avisos.Add(Crear(regla, esCritico ? Critico : Atencion,
                $"Las reparaciones de {fila.TipoMaquina} tardan {Formatear(aumento)}% más que su promedio",
                $"En los últimos {ventana} días el tiempo medio de reparación fue de {Formatear(reciente)} h ({fila.MuestraReciente} órdenes), frente a {Formatear(historico)} h del historial anterior ({fila.MuestraHistorica} órdenes).",
                aumento, (double)(esCritico ? regla.UmbralCritico!.Value : regla.Umbral), "%", ventana, fila.MuestraReciente));
        }

        return avisos;
    }

    /// <summary>R13: pocas máquinas concentran la mayor parte de las horas de parada.</summary>
    private async Task<List<AvisoDto>> ParadaConcentradaAsync(ReglaAviso regla, CancellationToken cancelacion)
    {
        var ventana = regla.VentanaDias ?? 180;
        var filas = await dao.ObtenerParadaPorActivoAsync(ventana, cancelacion);
        var total = filas.Sum(fila => fila.Horas);
        if (filas.Count == 0 || filas.Count < regla.MinimoDatos || total <= 0)
        {
            return [];
        }

        var cantidadDelVeintePorCiento = Math.Max(1, (int)Math.Ceiling(filas.Count * 0.2));
        var concentrado = Math.Round(filas.Take(cantidadDelVeintePorCiento).Sum(fila => fila.Horas) * 100.0 / total, 1);
        if (concentrado < (double)regla.Umbral)
        {
            return [];
        }

        var esCritico = regla.UmbralCritico is { } critico && concentrado >= (double)critico;
        return [Crear(regla, esCritico ? Critico : Atencion,
            $"El 20% de las máquinas concentra el {Formatear(concentrado)}% de la parada",
            $"{cantidadDelVeintePorCiento} de {filas.Count} máquinas con correctivas cerradas en los últimos {ventana} días suman {Formatear(concentrado)}% de las {Formatear(total)} horas de parada.",
            concentrado, (double)(esCritico ? regla.UmbralCritico!.Value : regla.Umbral), "%", ventana, filas.Count)];
    }

    /// <summary>R17: órdenes de prioridad alta siguen sin técnico asignado.</summary>
    private async Task<List<AvisoDto>> CriticasSinAsignarAsync(ReglaAviso regla, CancellationToken cancelacion)
    {
        var horas = (int)regla.Umbral;
        var horasCritico = regla.UmbralCritico is { } critico ? (int)critico : int.MaxValue;
        var fila = await dao.ContarCriticasSinAsignarAsync(horas, horasCritico, cancelacion);
        if (fila.Cantidad == 0 || fila.Cantidad < regla.MinimoDatos)
        {
            return [];
        }

        return [Crear(regla, fila.Criticas > 0 ? Critico : Atencion,
            $"{fila.Cantidad} órdenes de prioridad alta siguen sin técnico asignado",
            $"Pasaron más de {horas} horas desde su creación y todavía no tienen técnico.",
            fila.Cantidad, horas, "órdenes", null, fila.Cantidad, null, "pendiente")];
    }

    /// <summary>R18: órdenes abiertas sin ningún cambio de estado hace N días.</summary>
    private async Task<List<AvisoDto>> OrdenesSinMovimientoAsync(ReglaAviso regla, CancellationToken cancelacion)
    {
        var dias = (int)regla.Umbral;
        var diasCritico = regla.UmbralCritico is { } critico ? (int)critico : int.MaxValue;
        var fila = await dao.ContarSinMovimientoAsync("pendiente,en_curso", dias, diasCritico, cancelacion);
        if (fila.Cantidad == 0 || fila.Cantidad < regla.MinimoDatos)
        {
            return [];
        }

        return [Crear(regla, fila.Criticas > 0 ? Critico : Atencion,
            $"{fila.Cantidad} órdenes abiertas llevan más de {dias} días sin cambios",
            $"Órdenes pendientes o en curso sin ningún cambio de estado desde hace más de {dias} días.",
            fila.Cantidad, dias, "órdenes", null, fila.Cantidad)];
    }

    private static AvisoDto Crear(ReglaAviso regla, string severidad, string titulo, string detalle, double valor,
        double umbral, string unidad, int? ventana, int muestra, long? activoId = null, string? enlaceEstado = null) => new()
        {
            Regla = regla.Codigo,
            Severidad = severidad,
            Titulo = titulo,
            Detalle = detalle,
            Evidencia = new EvidenciaDeAvisoDto { Valor = valor, Umbral = umbral, Unidad = unidad, VentanaDias = ventana, Muestra = muestra },
            ActivoId = activoId,
            EnlaceEstado = enlaceEstado,
        };

    private static string Formatear(double valor) => valor.ToString("0.#", Cultura);

    private static string Meses(int dias) => dias >= 60 ? $"{Math.Round(dias / 30.0)} meses" : $"{dias} días";
}
