using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

public sealed record EstadoDeTerminos(
    TerminosCondiciones? Vigente, int? VersionAceptada, DateTime? AceptadoEn, bool AceptoLaVigente);

public enum ResultadoDeAceptacion
{
    Registrada,
    YaAceptada,
    UsuarioInexistente,
    TerminosDesactualizados,
}

public class TerminosDAO(SotraContext contexto)
{
    // Serializa la modificación y la aceptación para no aceptar contenido que acaba de cambiar.
    private const long ClaveDeBloqueoDePublicacion = 7_201_017;

    public Task<TerminosCondiciones?> ObtenerVigenteAsync(CancellationToken cancelacion) =>
        contexto.TerminosCondiciones.AsNoTracking().FirstOrDefaultAsync(t => t.Vigente, cancelacion);

    /// <summary>Modifica el único documento y reinicia las aceptaciones si cambió el contenido.</summary>
    public async Task<TerminosCondiciones> PublicarAsync(
        string texto, CancellationToken cancelacion)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({ClaveDeBloqueoDePublicacion})", cancelacion);

        var documento = await contexto.TerminosCondiciones.SingleOrDefaultAsync(t => t.Vigente, cancelacion);
        if (documento is null)
        {
            documento = new TerminosCondiciones { Version = 1, Vigente = true, Titulo = "Términos y condiciones", DatosAlmacenados = "" };
            contexto.TerminosCondiciones.Add(documento);
        }
        else if (documento.Texto != texto)
        {
            documento.Version++;
            documento.CreadoEn = DateTime.UtcNow;
            await contexto.AceptacionesTerminos.Where(a => a.IdTerminos == documento.IdTerminos).ExecuteDeleteAsync(cancelacion);
        }

        documento.Texto = texto;
        await contexto.SaveChangesAsync(cancelacion);
        await transaccion.CommitAsync(cancelacion);
        contexto.ChangeTracker.Clear();
        return documento;
    }

    public async Task<EstadoDeTerminos> ObtenerEstadoAsync(long idUsuario, CancellationToken cancelacion)
    {
        var estado = await contexto.TerminosCondiciones.AsNoTracking().Where(t => t.Vigente)
            .Select(t => new
            {
                Documento = t,
                AceptadoEn = contexto.AceptacionesTerminos
                    .Where(a => a.IdUsuario == idUsuario && a.IdTerminos == t.IdTerminos)
                    .Select(a => (DateTime?)a.AceptadoEn).FirstOrDefault()
            }).SingleOrDefaultAsync(cancelacion);
        return new EstadoDeTerminos(estado?.Documento,
            estado?.AceptadoEn is not null ? estado.Documento.Version : null,
            estado?.AceptadoEn, estado?.AceptadoEn is not null);
    }

    /// <summary>Registra que la persona aceptó la versión vigente. Repetirla no duplica el registro.</summary>
    public async Task<ResultadoDeAceptacion> AceptarAsync(long idUsuario, long idTerminos, int version, CancellationToken cancelacion)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancelacion);
        await contexto.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({ClaveDeBloqueoDePublicacion})", cancelacion);
        if (!await contexto.Usuarios.AsNoTracking().AnyAsync(u => u.IdUsuario == idUsuario, cancelacion))
        {
            return ResultadoDeAceptacion.UsuarioInexistente;
        }

        var vigente = await ObtenerVigenteAsync(cancelacion);
        if (vigente is null || vigente.IdTerminos != idTerminos || vigente.Version != version)
        {
            return ResultadoDeAceptacion.TerminosDesactualizados;
        }

        var filas = await contexto.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO operativo.aceptacion_terminos (id_usuario, id_terminos)
            VALUES ({idUsuario}, {idTerminos})
            ON CONFLICT (id_usuario, id_terminos) DO NOTHING", cancelacion);
        await transaccion.CommitAsync(cancelacion);
        return filas == 0 ? ResultadoDeAceptacion.YaAceptada : ResultadoDeAceptacion.Registrada;
    }
}
