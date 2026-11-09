namespace Api.Servicios;

/// <summary>
/// Almacenamiento de archivos fuera de la base de datos (Cloudflare R2). Solo se registra cuando está configurado;
/// sin él, los archivos de las evidencias se siguen guardando en la base.
/// </summary>
public interface IAlmacenDeArchivos
{
    Task GuardarAsync(string clave, byte[] contenido, string tipoContenido, CancellationToken cancelacion);

    /// <summary>Devuelve el contenido del archivo, o null si no existe.</summary>
    Task<byte[]?> LeerAsync(string clave, CancellationToken cancelacion);

    Task BorrarAsync(string clave, CancellationToken cancelacion);
}

/// <summary>El almacén de archivos no respondió o rechazó la operación.</summary>
public sealed class AlmacenNoDisponibleException : Exception
{
    public AlmacenNoDisponibleException(string mensaje, Exception? causa = null) : base(mensaje, causa)
    {
    }
}
