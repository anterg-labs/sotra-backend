using System.Text.RegularExpressions;
using Api.Infraestructura;
using Api.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;

namespace Api.Controladores;

public sealed class VisibilidadReporteFila
{
    public string Codigo { get; set; } = "";
    public bool Visible { get; set; }
}

public sealed record ActualizarVisibilidadReporteDto(bool? Visible);

/// <summary>Preferencias globales para mostrar u ocultar cada elemento de Reportes sin cambiar sus datos.</summary>
[ApiController]
[Route("api/v1/reportes/visibilidad")]
public sealed class VisibilidadReportesController(SotraContext contexto) : ControllerBase
{
    private static readonly Regex CodigoValido = new("^[a-z][a-z0-9_]{1,99}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Lista las preferencias guardadas; un elemento sin fila se muestra.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.ReporteConsultar)]
    [ProducesResponseType<List<VisibilidadReporteFila>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<VisibilidadReporteFila>>> Listar(CancellationToken cancelacion) =>
        Ok(await contexto.Database.SqlQueryRaw<VisibilidadReporteFila>("""
            SELECT codigo AS "Codigo", visible AS "Visible"
            FROM operativo.visibilidad_reporte ORDER BY codigo
            """).ToListAsync(cancelacion));

    /// <summary>Muestra u oculta un elemento de Reportes para todas las personas. Solo administración.</summary>
    [HttpPut("{codigo}")]
    [Authorize(Policy = Politicas.AvisoConfigurar)]
    [ProducesResponseType<VisibilidadReporteFila>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<VisibilidadReporteFila>> Actualizar(string codigo,
        [FromBody] ActualizarVisibilidadReporteDto datos, CancellationToken cancelacion)
    {
        if (!CodigoValido.IsMatch(codigo) || datos.Visible is null)
            return this.RespuestaDeError(400, "VISIBILIDAD_INVALIDA", "Indicá un elemento y su estado.");
        await contexto.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO operativo.visibilidad_reporte (codigo, visible) VALUES ({codigo}, {datos.Visible.Value})
            ON CONFLICT (codigo) DO UPDATE SET visible = EXCLUDED.visible
            """, cancelacion);
        return Ok(new VisibilidadReporteFila { Codigo = codigo, Visible = datos.Visible.Value });
    }
}
