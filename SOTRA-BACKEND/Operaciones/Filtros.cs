namespace SOTRA_BACKEND.Operaciones;

public sealed record FiltroClientes(string? Buscar, bool? Estado, bool Descendente);

public sealed record FiltroTiposMaquina(string? Buscar, bool? Estado);

public sealed record FiltroModelosMaquina(string? Buscar, long? TipoMaquinaId, bool? Estado);

public sealed record FiltroActivos(string? Buscar, long? ClienteId, long? ModeloMaquinaId, bool? Estado);
