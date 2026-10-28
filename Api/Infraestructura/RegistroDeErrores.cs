using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using Serilog.Core;
using Serilog.Events;

namespace Api.Infraestructura;

/// <summary>Metadatos mínimos para diagnosticar un error sin copiar cuerpos, tokens ni mensajes libres.</summary>
public sealed record ErrorRegistrable(DateTimeOffset Fecha, string Origen, string Categoria, string Codigo,
    string? Excepcion, int? EstadoHttp, string? Ruta, string? IdCorrelacion);

/// <summary>Guarda errores en PostgreSQL y conserva una copia pendiente si la base está temporalmente caída.</summary>
public sealed class RegistroDeErrores
{
    private readonly string _carpeta = Environment.GetEnvironmentVariable("SOTRA_ERROR_SPOOL_DIR")
        ?? Path.Combine(Path.GetTempPath(), "sotra-errores-pendientes");
    private readonly object _sincronizacion = new();

    public void Guardar(ErrorRegistrable error)
    {
        try
        {
            Insertar(error);
        }
        catch
        {
            // El registro no debe sustituir el error original ni iniciar una recursión del logger.
            try
            {
                Directory.CreateDirectory(_carpeta);
                var archivo = Path.Combine(_carpeta, $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.json");
                File.WriteAllText(archivo, JsonSerializer.Serialize(error));
            }
            catch { /* El log de consola de Serilog queda como último respaldo. */ }
        }
    }

    public void ReintentarPendientes()
    {
        lock (_sincronizacion)
        {
            if (!Directory.Exists(_carpeta)) return;
            foreach (var archivo in Directory.EnumerateFiles(_carpeta, "*.json").OrderBy(ruta => ruta))
            {
                try
                {
                    var error = JsonSerializer.Deserialize<ErrorRegistrable>(File.ReadAllText(archivo));
                    if (error is null) continue;
                    Insertar(error);
                    File.Delete(archivo);
                }
                catch { break; }
            }
        }
    }

    public void EliminarAntiguos()
    {
        try
        {
            using var enlace = CrearConexion();
            enlace.Open();
            using var comando = new NpgsqlCommand("DELETE FROM operativo.registro_error WHERE fecha < now() - INTERVAL '90 days'", enlace);
            comando.ExecuteNonQuery();
        }
        catch { /* Se reintentará en el siguiente ciclo de mantenimiento. */ }
    }

    private static void Insertar(ErrorRegistrable error)
    {
        using var enlace = CrearConexion();
        enlace.Open();
        using var comando = new NpgsqlCommand("""
            INSERT INTO operativo.registro_error
                (fecha, origen, categoria, codigo, excepcion, estado_http, ruta, id_correlacion)
            VALUES (@fecha, @origen, @categoria, @codigo, @excepcion, @estado, @ruta, @correlacion)
            """, enlace);
        comando.Parameters.AddWithValue("fecha", error.Fecha);
        comando.Parameters.AddWithValue("origen", Limitar(error.Origen, 20)!);
        comando.Parameters.AddWithValue("categoria", Limitar(error.Categoria, 160)!);
        comando.Parameters.AddWithValue("codigo", Limitar(error.Codigo, 80)!);
        comando.Parameters.Add(new NpgsqlParameter("excepcion", NpgsqlDbType.Varchar) { Value = (object?)Limitar(error.Excepcion, 160) ?? DBNull.Value });
        comando.Parameters.Add(new NpgsqlParameter("estado", NpgsqlDbType.Integer) { Value = (object?)error.EstadoHttp ?? DBNull.Value });
        comando.Parameters.Add(new NpgsqlParameter("ruta", NpgsqlDbType.Varchar) { Value = (object?)Limitar(error.Ruta?.Split('?')[0], 250) ?? DBNull.Value });
        comando.Parameters.Add(new NpgsqlParameter("correlacion", NpgsqlDbType.Varchar) { Value = (object?)Limitar(error.IdCorrelacion, 64) ?? DBNull.Value });
        comando.ExecuteNonQuery();
    }

    private static NpgsqlConnection CrearConexion()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST");
        var baseDeDatos = Environment.GetEnvironmentVariable("DB_NAME");
        var usuario = Environment.GetEnvironmentVariable("DB_USER");
        var contrasena = Environment.GetEnvironmentVariable("DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(baseDeDatos) ||
            string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(contrasena))
            throw new InvalidOperationException("Base de errores no configurada.");
        var conexion = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Port = int.TryParse(Environment.GetEnvironmentVariable("DB_PORT"), out var puerto) ? puerto : 5432,
            Database = baseDeDatos,
            Username = usuario,
            Password = contrasena,
            Timeout = 2,
            CommandTimeout = 2,
        };
        return new NpgsqlConnection(conexion.ConnectionString);
    }

    private static string? Limitar(string? valor, int longitud) => valor is null ? null : valor[..Math.Min(valor.Length, longitud)];
}

/// <summary>Persiste todos los eventos de nivel Error o Fatal emitidos mediante Serilog.</summary>
public sealed class SumideroDeErrores(RegistroDeErrores registro) : ILogEventSink
{
    [ThreadStatic] private static bool _guardando;

    public void Emit(LogEvent evento)
    {
        if (evento.Level < LogEventLevel.Error || _guardando) return;
        _guardando = true;
        try
        {
            registro.Guardar(new ErrorRegistrable(DateTimeOffset.UtcNow, "api",
                Propiedad(evento, "SourceContext") ?? "api",
                Propiedad(evento, "Codigo") ?? (evento.Exception is null ? "LOG_ERROR" : "EXCEPCION"),
                evento.Exception?.GetType().Name,
                int.TryParse(Propiedad(evento, "Estado") ?? Propiedad(evento, "StatusCode"), out var estado) ? estado : null,
                Propiedad(evento, "Ruta") ?? Propiedad(evento, "RequestPath"), Propiedad(evento, "IdCorrelacion")));
        }
        finally { _guardando = false; }
    }

    private static string? Propiedad(LogEvent evento, string nombre) =>
        evento.Properties.TryGetValue(nombre, out var valor) && valor is ScalarValue { Value: not null } escalar
            ? Convert.ToString(escalar.Value) : null;
}

/// <summary>Reenvía al recuperarse la base los errores conservados durante una caída.</summary>
public sealed class ReenvioDeErrores(RegistroDeErrores registro) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancelacion)
    {
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(30));
        var ciclos = 0;
        registro.EliminarAntiguos();
        while (await temporizador.WaitForNextTickAsync(cancelacion))
        {
            registro.ReintentarPendientes();
            if (++ciclos % 2880 == 0) registro.EliminarAntiguos();
        }
    }
}
