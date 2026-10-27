namespace SOTRA_BACKEND.Operaciones;

public sealed record FiltroClientes(string? Buscar, bool? Estado, bool Descendente, string? OrdenarPor = null, bool ConActivos = false);

public sealed record FiltroTiposMaquina(string? Buscar, bool? Estado, string? OrdenarPor = null, bool Descendente = false);

public sealed record FiltroModelosMaquina(string? Buscar, long? TipoMaquinaId, bool? Estado, string? OrdenarPor = null, bool Descendente = false);

public sealed record FiltroActivos(string? Buscar, long? ClienteId, long? ModeloMaquinaId, bool? Estado, string? OrdenarPor = null, bool Descendente = false);

public sealed record FiltroRepuestos(
    string? Buscar, bool? Estado, string? Ubicacion, bool? BajoStock,
    string? EstadoStock = null, bool OrdenarPorCriticidad = false, string? OrdenarPor = null, bool Descendente = false);

public sealed record FiltroMovimientosStock(
    string? Buscar, long? RepuestoId, string? Tipo, DateOnly? FechaDesde, DateOnly? FechaHasta,
    string? Ubicacion, long? UsuarioId, string? OrdenarPor = null, bool Descendente = false);

public sealed record FiltroOrdenesTrabajo(
    string? Buscar, long? ClienteId, long? ActivoId, long? TecnicoId, string? Estado, string? Prioridad,
    bool? EsPreventivo, DateOnly? FechaDesde, DateOnly? FechaHasta, bool? ConSugerencias,
    bool OrdenarPorCreacion, bool Descendente, string? OrdenarPor = null, string? Fase = null,
    bool? SoloActivas = null);
