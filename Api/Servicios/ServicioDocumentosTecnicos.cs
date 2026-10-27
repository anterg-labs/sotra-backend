using System.Security.Cryptography;
using Api.DTOs.DocumentosTecnicos;
using Npgsql;
using SOTRA_BACKEND.Operaciones;
using Pgvector;

namespace Api.Servicios;

public class ServicioDocumentosTecnicos : IAsyncDisposable
{
    // pdf-v5: quita encabezados, pies y etiquetas de figura repetidos. Los documentos procesados con una versión
    // anterior se actualizan con POST /documentos-tecnicos/{id}/reprocesar.
    private const string VersionDelProcesamiento = "bge-m3:1024:pdf-v5:1400:200";
    private const int FragmentosPorPedidoDeEmbeddings = 8;

    private const string ColumnasDeDocumento = """
        SELECT documento.id, documento.nombre_archivo, documento.activo_id,
               activo.nombre, activo.num_serie, documento.usa_sugerencias_ia,
               documento.cantidad_fragmentos, documento.creado_en,
               COALESCE(documento.titulo, documento.nombre_archivo), documento.tipo, documento.descripcion,
               documento.vigente, documento.modelo_maquina_id, modelo.nombre

        """;

    /// <summary>
    /// Lista cerrada de columnas ordenables y su expresión SQL fija. La clave que llega de la API solo se busca aquí:
    /// nunca se concatena texto del cliente en la consulta.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ColumnasOrdenables = new Dictionary<string, string>
    {
        ["titulo"] = "lower(COALESCE(documento.titulo, documento.nombre_archivo)) COLLATE \"es-UY-x-icu\"",
        ["tipo"] = "lower(documento.tipo) COLLATE \"es-UY-x-icu\"",
        ["vigente"] = "documento.vigente",
        ["nombreAsociado"] = "lower(COALESCE(activo.nombre, activo.num_serie, modelo.nombre)) COLLATE \"es-UY-x-icu\"",
        ["usaSugerenciasIA"] = "documento.usa_sugerencias_ia",
        ["creadoEn"] = "documento.creado_en",
    };

    private readonly ServicioEmbeddings _embeddings;
    private readonly NpgsqlDataSource _baseDatos;

    public ServicioDocumentosTecnicos(ServicioEmbeddings embeddings)
    {
        _embeddings = embeddings;
        _baseDatos = BaseVectorial.CrearConexion();
    }

    private const string ConsultaBaseDeDocumentos = """
        FROM vectorial.documento_tecnico AS documento
        LEFT JOIN operativo.activo AS activo ON activo.id_activo = documento.activo_id
        LEFT JOIN operativo.modelo_maquina AS modelo ON modelo.id_modelo_maquina = documento.modelo_maquina_id

        """;

    /// <summary>Lista una página de documentos filtrando y contando en la base, sin traer el archivo.</summary>
    public async Task<(List<DocumentoTecnicoDto> Elementos, int Total)> ListarPaginaAsync(
        string? buscar, long? activoId, string? tipo, bool? vigente, long? modeloMaquinaId,
        int pagina, int tamanoPagina, CancellationToken cancellationToken, string? ordenarPor = null, bool descendente = false)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);

        var sentido = descendente ? "DESC" : "ASC";
        var orden = ordenarPor is not null && ColumnasOrdenables.TryGetValue(ordenarPor, out var expresion)
            ? $"{expresion} {sentido} NULLS LAST, documento.id {sentido}"
            : "documento.id";

        string? patron = string.IsNullOrWhiteSpace(buscar) ? null : ConsultasDeTexto.PatronContiene(buscar);
        const string filtros = """
            WHERE ($1::text IS NULL OR documento.nombre_archivo ILIKE $1
                   OR documento.titulo ILIKE $1 OR documento.descripcion ILIKE $1
                   OR COALESCE(activo.nombre, activo.num_serie) ILIKE $1)
              AND ($2::bigint IS NULL OR documento.activo_id = $2)
              AND ($3::text IS NULL OR documento.tipo = $3)
              AND ($4::boolean IS NULL OR documento.vigente = $4)
              AND ($5::bigint IS NULL OR documento.modelo_maquina_id = $5)
            """;

        await using var contar = _baseDatos.CreateCommand("SELECT count(*) " + ConsultaBaseDeDocumentos + filtros);
        contar.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)patron ?? DBNull.Value);
        contar.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, (object?)activoId ?? DBNull.Value);
        contar.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)tipo ?? DBNull.Value);
        contar.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Boolean, (object?)vigente ?? DBNull.Value);
        contar.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, (object?)modeloMaquinaId ?? DBNull.Value);
        var total = Convert.ToInt32(await contar.ExecuteScalarAsync(cancellationToken));

        await using var comando = _baseDatos.CreateCommand(ColumnasDeDocumento + ConsultaBaseDeDocumentos + filtros + " ORDER BY " + orden + """

            LIMIT $6 OFFSET $7
            """);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)patron ?? DBNull.Value);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, (object?)activoId ?? DBNull.Value);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)tipo ?? DBNull.Value);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Boolean, (object?)vigente ?? DBNull.Value);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Bigint, (object?)modeloMaquinaId ?? DBNull.Value);
        comando.Parameters.AddWithValue(tamanoPagina);
        comando.Parameters.AddWithValue((pagina - 1) * tamanoPagina);

        var documentos = new List<DocumentoTecnicoDto>();
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        while (await lector.ReadAsync(cancellationToken))
        {
            documentos.Add(LeerDocumento(lector));
        }

        return (documentos, total);
    }

    public async Task<DocumentoTecnicoDto?> ObtenerAsync(long id, CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);
        await using var comando = _baseDatos.CreateCommand(
            ColumnasDeDocumento + ConsultaBaseDeDocumentos + "WHERE documento.id = $1");
        comando.Parameters.AddWithValue(id);
        await using var lector = await comando.ExecuteReaderAsync(cancellationToken);
        return await lector.ReadAsync(cancellationToken) ? LeerDocumento(lector) : null;
    }

    public async Task<DocumentoTecnicoDto> GuardarAsync(string nombreArchivo, DatosDelDocumento datosSolicitados,
        bool usaSugerenciasIa, byte[] archivo, CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);
        var datos = datosSolicitados.Normalizar();
        TiposDeDocumento.Validar(datos);
        await ValidarActivoAsync(datos.ActivoId, cancellationToken);
        await ValidarModeloAsync(datos.ModeloMaquinaId, cancellationToken);

        var fragmentos = new List<FragmentoDocumento>();
        var vectores = new List<float[]>();
        if (usaSugerenciasIa)
        {
            fragmentos = ExtraerFragmentos(archivo);
            vectores = CrearVectores(fragmentos, cancellationToken);
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
                 archivo, texto, creado_en, documento_vectorial_id,
                 titulo, tipo, descripcion, modelo_maquina_id)
            VALUES ($1, $2, $3, $4, $5, '', $6, $7, $8, $9, $10, $11)
            RETURNING id
            """, conexion, transaccion);
        comando.Parameters.AddWithValue(nombreArchivo);
        comando.Parameters.AddWithValue(ValorONuloParaBase(datos.ActivoId));
        comando.Parameters.AddWithValue(usaSugerenciasIa);
        comando.Parameters.AddWithValue(fragmentos.Count);
        comando.Parameters.AddWithValue(archivo);
        comando.Parameters.AddWithValue(DateTime.UtcNow);
        comando.Parameters.AddWithValue(ValorONuloParaBase(documentoVectorialId));
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)datos.Titulo ?? DBNull.Value);
        comando.Parameters.AddWithValue(datos.Tipo);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)datos.Descripcion ?? DBNull.Value);
        comando.Parameters.AddWithValue(ValorONuloParaBase(datos.ModeloMaquinaId));

        var id = Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken));
        await transaccion.CommitAsync(cancellationToken);

        return (await ObtenerAsync(id, cancellationToken))!;
    }

    /// <summary>Reemplaza los datos generales del documento; el archivo y el procesamiento de IA no cambian.</summary>
    public async Task<DocumentoTecnicoDto?> ActualizarAsync(long id, DatosDelDocumento datosSolicitados,
        CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);
        var datos = datosSolicitados.Normalizar();
        TiposDeDocumento.Validar(datos);
        await ValidarActivoAsync(datos.ActivoId, cancellationToken);
        await ValidarModeloAsync(datos.ModeloMaquinaId, cancellationToken);

        await using var comando = _baseDatos.CreateCommand("""
            UPDATE vectorial.documento_tecnico
            SET titulo = $2, tipo = $3, descripcion = $4, activo_id = $5, modelo_maquina_id = $6
            WHERE id = $1
            """);
        comando.Parameters.AddWithValue(id);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)datos.Titulo ?? DBNull.Value);
        comando.Parameters.AddWithValue(datos.Tipo);
        comando.Parameters.AddWithValue(NpgsqlTypes.NpgsqlDbType.Text, (object?)datos.Descripcion ?? DBNull.Value);
        comando.Parameters.AddWithValue(ValorONuloParaBase(datos.ActivoId));
        comando.Parameters.AddWithValue(ValorONuloParaBase(datos.ModeloMaquinaId));
        return await comando.ExecuteNonQueryAsync(cancellationToken) == 0 ? null : await ObtenerAsync(id, cancellationToken);
    }

    /// <summary>
    /// Vuelve a extraer los fragmentos del PDF guardado con la versión actual del procesamiento y reemplaza los
    /// anteriores. El documento conserva su id y sus datos generales. Devuelve <c>null</c> si no existe.
    /// </summary>
    public async Task<DocumentoTecnicoDto?> ReprocesarAsync(long id, CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);
        byte[]? archivo = null;
        string nombreArchivo;
        long? documentoVectorialAnterior = null;
        await using (var leer = _baseDatos.CreateCommand("""
            SELECT archivo, nombre_archivo, usa_sugerencias_ia, documento_vectorial_id
            FROM vectorial.documento_tecnico WHERE id = $1
            """))
        {
            leer.Parameters.AddWithValue(id);
            await using var lector = await leer.ExecuteReaderAsync(cancellationToken);
            if (!await lector.ReadAsync(cancellationToken))
            {
                return null;
            }

            if (!lector.GetBoolean(2))
            {
                throw new ConflictoDeDatosException("DOCUMENTO_SIN_SUGERENCIAS_IA",
                    "El documento no se usa para las sugerencias de IA: no hay nada que reprocesar.");
            }

            if (!lector.IsDBNull(0))
            {
                archivo = (byte[])lector[0];
            }

            nombreArchivo = lector.GetString(1);
            if (!lector.IsDBNull(3))
            {
                documentoVectorialAnterior = lector.GetInt64(3);
            }
        }

        if (archivo is null || archivo.Length == 0)
        {
            throw new ConflictoDeDatosException("DOCUMENTO_SIN_ARCHIVO",
                "El documento no tiene el PDF guardado. Hay que volver a subirlo.");
        }

        var fragmentos = ExtraerFragmentos(archivo);
        var vectores = CrearVectores(fragmentos, cancellationToken);
        var huella = Convert.ToHexString(SHA256.HashData(archivo));

        await using var conexion = await _baseDatos.OpenConnectionAsync(cancellationToken);
        await using var transaccion = await conexion.BeginTransactionAsync(cancellationToken);
        var documentoVectorialNuevo = await InsertarFragmentosAsync(conexion, transaccion,
            nombreArchivo, huella, fragmentos, vectores, cancellationToken);

        await using (var actualizar = new NpgsqlCommand("""
            UPDATE vectorial.documento_tecnico
            SET documento_vectorial_id = $2, cantidad_fragmentos = $3
            WHERE id = $1
            """, conexion, transaccion))
        {
            actualizar.Parameters.AddWithValue(id);
            actualizar.Parameters.AddWithValue(documentoVectorialNuevo);
            actualizar.Parameters.AddWithValue(fragmentos.Count);
            await actualizar.ExecuteNonQueryAsync(cancellationToken);
        }

        // Otro documento técnico puede compartir los fragmentos anteriores (el mismo PDF subido dos veces).
        if (documentoVectorialAnterior.HasValue)
        {
            await using var borrar = new NpgsqlCommand("""
                DELETE FROM vectorial.documentos
                WHERE id = $1
                  AND NOT EXISTS (SELECT 1 FROM vectorial.documento_tecnico WHERE documento_vectorial_id = $1)
                """, conexion, transaccion);
            borrar.Parameters.AddWithValue(documentoVectorialAnterior.Value);
            await borrar.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaccion.CommitAsync(cancellationToken);
        return await ObtenerAsync(id, cancellationToken);
    }

    /// <summary>Marca un documento como vigente u obsoleto. Las sugerencias de IA solo usan los vigentes.</summary>
    public async Task<DocumentoTecnicoDto?> CambiarVigenciaAsync(long id, bool vigente, CancellationToken cancellationToken)
    {
        await BaseVectorial.PrepararTablasAsync(_baseDatos, cancellationToken);
        await using var comando = _baseDatos.CreateCommand(
            "UPDATE vectorial.documento_tecnico SET vigente = $2 WHERE id = $1");
        comando.Parameters.AddWithValue(id);
        comando.Parameters.AddWithValue(vigente);
        return await comando.ExecuteNonQueryAsync(cancellationToken) == 0 ? null : await ObtenerAsync(id, cancellationToken);
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

    // Los embeddings se calculan en la API por lotes; entre lote y lote se respeta la cancelación del pedido.
    private List<float[]> CrearVectores(List<FragmentoDocumento> fragmentos, CancellationToken cancellationToken)
    {
        var vectores = new List<float[]>();
        for (var inicio = 0; inicio < fragmentos.Count; inicio += FragmentosPorPedidoDeEmbeddings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var textosDelLote = new List<string>();
            var fin = Math.Min(inicio + FragmentosPorPedidoDeEmbeddings, fragmentos.Count);
            for (var posicion = inicio; posicion < fin; posicion++)
            {
                textosDelLote.Add(fragmentos[posicion].Texto);
            }

            vectores.AddRange(_embeddings.Crear(textosDelLote));
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
            throw new ValidacionDeNegocioException("El activo indicado no existe.", "ACTIVO_NO_EXISTE");
        }
    }

    private async Task ValidarModeloAsync(long? modeloMaquinaId, CancellationToken cancellationToken)
    {
        if (!modeloMaquinaId.HasValue)
        {
            return;
        }

        await using var comando = _baseDatos.CreateCommand(
            "SELECT COUNT(*) FROM operativo.modelo_maquina WHERE id_modelo_maquina = $1");
        comando.Parameters.AddWithValue(modeloMaquinaId.Value);
        if (Convert.ToInt64(await comando.ExecuteScalarAsync(cancellationToken)) == 0)
        {
            throw new ValidacionDeNegocioException("El modelo de máquina indicado no existe.", "MODELO_NO_EXISTE");
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
            CreadoEn = lector.GetDateTime(7),
            Titulo = lector.GetString(8),
            Tipo = lector.GetString(9),
            Descripcion = lector.IsDBNull(10) ? null : lector.GetString(10),
            Vigente = lector.GetBoolean(11),
            ModeloMaquinaId = lector.IsDBNull(12) ? null : lector.GetInt64(12),
            NombreModelo = lector.IsDBNull(13) ? null : lector.GetString(13)
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
