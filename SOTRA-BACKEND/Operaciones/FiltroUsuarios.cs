namespace SOTRA_BACKEND.Operaciones;

public sealed record FiltroUsuarios(string? Buscar, long? RolId, bool? Estado, bool Descendente, string? OrdenarPor = null);
