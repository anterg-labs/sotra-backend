using Microsoft.EntityFrameworkCore;
using SOTRA_BACKEND.Contexto;
using SOTRA_BACKEND.Modelos;

namespace SOTRA_BACKEND.Operaciones;

/// <summary>Avisos de la persona con sesión: solo ve y marca los suyos.</summary>
public class NotificacionDAO(SotraContext contexto)
{
    public async Task<(List<Notificacion> Elementos, int Total)> ListarPaginaAsync(
        long usuarioId, bool soloNoLeidas, int pagina, int tamanoPagina, CancellationToken cancelacion)
    {
        var consulta = contexto.Notificaciones.AsNoTracking().Where(n => n.IdUsuario == usuarioId);
        if (soloNoLeidas)
        {
            consulta = consulta.Where(n => n.LeidaEn == null);
        }

        return await ConsultasDeTexto.PaginarAsync(
            consulta.OrderByDescending(n => n.CreadaEn).ThenByDescending(n => n.IdNotificacion), pagina, tamanoPagina, cancelacion);
    }

    public Task<int> ContarNoLeidasAsync(long usuarioId, CancellationToken cancelacion) =>
        contexto.Notificaciones.CountAsync(n => n.IdUsuario == usuarioId && n.LeidaEn == null, cancelacion);

    /// <summary>Marca una notificación propia como leída; <c>false</c> si no existe o es de otra persona.</summary>
    public async Task<bool> MarcarLeidaAsync(long usuarioId, long id, CancellationToken cancelacion)
    {
        var existe = await contexto.Notificaciones.AnyAsync(n => n.IdNotificacion == id && n.IdUsuario == usuarioId, cancelacion);
        if (!existe)
        {
            return false;
        }

        await contexto.Notificaciones
            .Where(n => n.IdNotificacion == id && n.IdUsuario == usuarioId && n.LeidaEn == null)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(n => n.LeidaEn, DateTime.UtcNow), cancelacion);
        return true;
    }

    public Task<int> MarcarTodasLeidasAsync(long usuarioId, CancellationToken cancelacion) =>
        contexto.Notificaciones
            .Where(n => n.IdUsuario == usuarioId && n.LeidaEn == null)
            .ExecuteUpdateAsync(cambios => cambios.SetProperty(n => n.LeidaEn, DateTime.UtcNow), cancelacion);
}
