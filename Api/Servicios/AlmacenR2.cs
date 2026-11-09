using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Api.Servicios;

/// <summary>
/// Archivos en un bucket de Cloudflare R2, que usa el mismo protocolo que Amazon S3. El bucket es privado: los
/// archivos se leen siempre a través de la API, que controla quién puede verlos.
/// </summary>
public sealed class AlmacenR2 : IAlmacenDeArchivos, IDisposable
{
    private readonly AmazonS3Client _cliente;
    private readonly string _bucket;

    public AlmacenR2(string endpoint, string bucket, string idDeClave, string claveSecreta)
    {
        var configuracion = new AmazonS3Config
        {
            ServiceURL = endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
            // R2 no admite las sumas de verificación que el SDK agrega por defecto desde la versión 4.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            Timeout = TimeSpan.FromSeconds(30),
            MaxErrorRetry = 2,
        };
        _cliente = new AmazonS3Client(new BasicAWSCredentials(idDeClave, claveSecreta), configuracion);
        _bucket = bucket;
    }

    public async Task GuardarAsync(string clave, byte[] contenido, string tipoContenido, CancellationToken cancelacion)
    {
        using var flujo = new MemoryStream(contenido, writable: false);
        var solicitud = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = clave,
            InputStream = flujo,
            ContentType = tipoContenido,
            DisablePayloadSigning = true,
        };

        try
        {
            await _cliente.PutObjectAsync(solicitud, cancelacion);
        }
        catch (Exception error) when (error is AmazonServiceException or HttpRequestException)
        {
            throw new AlmacenNoDisponibleException($"No se pudo guardar {clave} en R2.", error);
        }
    }

    public async Task<byte[]?> LeerAsync(string clave, CancellationToken cancelacion)
    {
        try
        {
            using var respuesta = await _cliente.GetObjectAsync(_bucket, clave, cancelacion);
            using var memoria = new MemoryStream();
            await respuesta.ResponseStream.CopyToAsync(memoria, cancelacion);
            return memoria.ToArray();
        }
        catch (AmazonS3Exception error) when (error.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (Exception error) when (error is AmazonServiceException or HttpRequestException)
        {
            throw new AlmacenNoDisponibleException($"No se pudo leer {clave} de R2.", error);
        }
    }

    public async Task BorrarAsync(string clave, CancellationToken cancelacion)
    {
        try
        {
            await _cliente.DeleteObjectAsync(_bucket, clave, cancelacion);
        }
        catch (Exception error) when (error is AmazonServiceException or HttpRequestException)
        {
            throw new AlmacenNoDisponibleException($"No se pudo borrar {clave} de R2.", error);
        }
    }

    public void Dispose()
    {
        _cliente.Dispose();
    }
}
