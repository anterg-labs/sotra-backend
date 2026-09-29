using System.Security.Cryptography;
using Api.DTOs.DocumentosTecnicos;
using Npgsql;
using Pgvector;

namespace Api.Servicios;

public class ServicioDocumentosTecnicos : IAsyncDisposable
{
    private const string VersionDelProcesamiento = "bge-m3:1024:pdf-v3:1400:200";
    private const int FragmentosPorPedidoDeEmbeddings = 8;

    private readonly ServicioOllama _ollama;
    private readonly NpgsqlDataSource _baseDatos;

    public ServicioDocumentosTecnicos(ServicioOllama ollama)
    {
        _ollama = ollama;
        _baseDatos = BaseVectorial.CrearConexion();
    }

    public async Task<List<DocumentoTecnicoDto>> ListarAsync(CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);
        var documentos = new List<DocumentoTecnicoDto>();
        await using var comando = _baseDatos.CreateCommand("""
            SELECT documento.id, documento.nombre_archivo, documento.activo_id,
                   activo.nombre, activo.num_serie, documento.usa_sugerencias_ia,
                   documento.cantidad_fragmentos, documento.creado_en
            FROM vectorial.documento_tecnico AS documento
            LEFT JOIN operativo.activo AS activo ON activo.id_activo = documento.activo_id
            ORDER BY documento.id
            """);

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            documentos.Add(LeerDocumento(lector));
        }

        return documentos;
    }

    public async Task<DocumentoTecnicoDto> GuardarAsync(string nombreArchivo, long? activoId,
        bool usaSugerenciasIa, byte[] archivo, CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);
        await ValidarActivoAsync(activoId, cancellationToken);

        var fragmentos = new List<FragmentoDocumento>();
        var vectores = new List<float[]>();
        if (usaSugerenciasIa)
        {
            fragmentos = ExtraerFragmentos(archivo);
            vectores = await CrearVectoresAsync(fragmentos, cancellationToken);
        }

        var huella = Convert.ToHexString(SHA256.HashData(archivo));
        await using var conexion = await _baseDatos.OpenConnectionAsync(cancellationToken);
        await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);

        long? documentoVectorialId = null;
        if (usaSugerenciasIa)
        {
            documentoVectorialId = await InsertarFragmentosAsync(conexion, transaccion,
                nombreArchivo, huella, fragmentos, vectores, cancellationToken);
        }

        await using var comando = new NpgsqlCommand("""
            INSERT INTO vectorial.documento_tecnico
                (nombre_archivo, activo_id, usa_sugerencias_ia, cantidad_fragmentos,
                 archivo, texto, creado_en, documento_vectorial_id)
            VALUES ($1, $2, $3, $4, $5, '', $6, $7)
            RETURNING id, creado_en
            """, conexion, transaccion);
        comando.Parameters.AddWithValue(nombreArchivo);
        comando.Parameters.AddWithValue(ValorONuloParaBase(activoId));
        comando.Parameters.AddWithValue(usaSugerenciasIa);
        comando.Parameters.AddWithValue(fragmentos.Count);
        comando.Parameters.AddWithValue(archivo);
        comando.Parameters.AddWithValue(DateTime.UtcNow);
        comando.Parameters.AddWithValue(ValorONuloParaBase(documentoVectorialId));

        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        await lector.ReadAsync(cancellationToken);
        var id = lector.GetInt64(0);
        var creadoEn = lector.GetDateTime(1);
        await lector.CloseAsync();
        await transaccion.CommitAsync(cancellationToken);

        var nombreActivo = await ObtenerNombreActivoAsync(activoId, cancellationToken);
        return new DocumentoTecnicoDto
        {
            Id = id,
            NombreArchivo = nombreArchivo,
            ActivoId = activoId,
            NombreActivo = nombreActivo,
            UsaSugerenciasIA = usaSugerenciasIa,
            CantidadFragmentos = fragmentos.Count,
            CreadoEn = creadoEn
        };
    }

    public async Task<ArchivoGuardado?> ObtenerArchivoAsync(long id, CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);
        await using var comando = _baseDatos.CreateCommand(
            "SELECT archivo, nombre_archivo FROM vectorial.documento_tecnico WHERE id = $1");
        comando.Parameters.AddWithValue(id);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        if (!await lector.ReadAsync(cancellationToken))
        {
            return null;
        }

        var contenido = (byte[])lector["archivo"];
        var nombreArchivo = lector.GetString(1);
        return new ArchivoGuardado(contenido, nombreArchivo);
    }

    public async Task<bool> EliminarAsync(long id, CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);
        await using var conexion = await _baseDatos.OpenConnectionAsync(cancellationToken);
        await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);

        long? documentoVectorialId = null;
        await using (var seleccionar = new NpgsqlCommand(
            "SELECT documento_vectorial_id FROM vectorial.documento_tecnico WHERE id = $1",
            conexion, transaccion))
        {
            seleccionar.Parameters.AddWithValue(id);
            var resultado = await seleccionar.ExecuteScalarAsync(cancellationToken);
            if (resultado is null)
            {
                return false;
            }

            if (resultado != DBNull.Value)
            {
                documentoVectorialId = Convert.ToInt64(resultado);
            }
        }

        await using (var borrar = new NpgsqlCommand(
            "DELETE FROM vectorial.documento_tecnico WHERE id = $1", conexion, transaccion))
        {
            borrar.Parameters.AddWithValue(id);
            await borrar.ExecuteNonQueryAsync(cancellationToken);
        }

        if (documentoVectorialId.HasValue)
        {
            await using var borrarFragmentos = new NpgsqlCommand(
                "DELETE FROM vectorial.documentos WHERE id = $1", conexion, transaccion);
            borrarFragmentos.Parameters.AddWithValue(documentoVectorialId.Value);
            await borrarFragmentos.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaccion.CommitAsync(cancellationToken);
        return true;
    }

    private static List<FragmentoDocumento> ExtraerFragmentos(byte[] archivo)
    {
        List<FragmentoDocumento> fragmentos;
        try
        {
            fragmentos = DocumentoPdf.Extraer(archivo);
        }
        catch
        {
            throw new InvalidDataException("PDF_INVALIDO");
        }

        if (fragmentos.Count == 0)
        {
            throw new InvalidDataException("PDF_SIN_TEXTO");
        }

        return fragmentos;
    }

    private async Task<List<float[]>> CrearVectoresAsync(List<FragmentoDocumento> fragmentos,
        CancellationToken cancellationToken)
    {
        var vectores = new List<float[]>();
        var textosDelPedido = new List<string>();
        foreach (var fragmento in fragmentos)
        {
            textosDelPedido.Add(fragmento.Texto);
            if (textosDelPedido.Count == FragmentosPorPedidoDeEmbeddings)
            {
                vectores.AddRange(await _ollama.CrearEmbeddingsAsync(textosDelPedido, cancellationToken));
                textosDelPedido = new List<string>();
            }
        }

        if (textosDelPedido.Count > 0)
        {
            vectores.AddRange(await _ollama.CrearEmbeddingsAsync(textosDelPedido, cancellationToken));
        }

        return vectores;
    }

    private static async Task<long> InsertarFragmentosAsync(NpgsqlConnection conexion,
        NpgsqlTransaction transaccion, string nombre, string huella,
        List<FragmentoDocumento> fragmentos, List<float[]> vectores,
        CancellationToken cancellationToken)
    {
        await using var documento = new NpgsqlCommand("""
            INSERT INTO vectorial.documentos(nombre, huella, version_proceso)
            VALUES ($1, $2, $3) RETURNING id
            """, conexion, transaccion);
        documento.Parameters.AddWithValue(nombre);
        documento.Parameters.AddWithValue(huella);
        documento.Parameters.AddWithValue(VersionDelProcesamiento);
        var documentoId = Convert.ToInt64(await documento.ExecuteScalarAsync(cancellationToken));

        for (var posicion = 0; posicion < fragmentos.Count; posicion++)
        {
            var fragmento = fragmentos[posicion];
            await using var comando = new NpgsqlCommand("""
                INSERT INTO vectorial.fragmentos(documento_id, posicion, pagina, texto, embedding)
                VALUES ($1, $2, $3, $4, $5)
                """, conexion, transaccion);
            comando.Parameters.AddWithValue(documentoId);
            comando.Parameters.AddWithValue(posicion);
            comando.Parameters.AddWithValue(fragmento.Pagina);
            comando.Parameters.AddWithValue(fragmento.Texto);
            comando.Parameters.AddWithValue(new Vector(vectores[posicion]));
            await comando.ExecuteNonQueryAsync(cancellationToken);
        }

        return documentoId;
    }

    private async Task ValidarActivoAsync(long? activoId, CancellationToken cancellationToken)
    {
        if (!activoId.HasValue)
        {
            return;
        }

        await using var comando = _baseDatos.CreateCommand(
            "SELECT COUNT(*) FROM operativo.activo WHERE id_activo = $1");
        comando.Parameters.AddWithValue(activoId.Value);
        var cantidad = Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken));
        if (cantidad == 0)
        {
            throw new ArgumentException("El activo indicado no existe.");
        }
    }

    private async Task<string?> ObtenerNombreActivoAsync(long? activoId, CancellationToken cancellationToken)
    {
        if (!activoId.HasValue)
        {
            return null;
        }

        await using var comando = _baseDatos.CreateCommand(
            "SELECT nombre, num_serie FROM operativo.activo WHERE id_activo = $1");
        comando.Parameters.AddWithValue(activoId.Value);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        await lector.ReadAsync(cancellationToken);

        string? nombre = null;
        if (!lector.IsDBNull(0))
        {
            nombre = lector.GetString(0);
        }

        if (!string.IsNullOrWhiteSpace(nombre))
        {
            return nombre;
        }

        return lector.GetString(1);
    }

    private static DocumentoTecnicoDto LeerDocumento(NpgsqlDataReader lector)
    {
        long? activoId = null;
        if (!lector.IsDBNull(2))
        {
            activoId = lector.GetInt64(2);
        }

        string? nombreActivo = null;
        if (!lector.IsDBNull(3))
        {
            nombreActivo = lector.GetString(3);
        }
        else if (!lector.IsDBNull(4))
        {
            nombreActivo = lector.GetString(4);
        }

        return new DocumentoTecnicoDto
        {
            Id = lector.GetInt64(0),
            NombreArchivo = lector.GetString(1),
            ActivoId = activoId,
            NombreActivo = nombreActivo,
            UsaSugerenciasIA = lector.GetBoolean(5),
            CantidadFragmentos = lector.GetInt32(6),
            CreadoEn = lector.GetDateTime(7)
        };
    }

    private static object ValorONuloParaBase(long? valor)
    {
        if (valor.HasValue)
        {
            return valor.Value;
        }

        return DBNull.Value;
    }

    public async ValueTask DisposeAsync()
    {
        await _baseDatos.DisposeAsync();
    }
}
