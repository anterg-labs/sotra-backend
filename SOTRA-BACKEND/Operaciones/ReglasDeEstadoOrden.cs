namespace SOTRA_BACKEND.Operaciones;

/// <summary>Vocabulario cerrado de las fases (columna <c>estado_ot.fase</c>); no son nombres de estado.</summary>
public static class FasesDeEstado
{
    public const string Pendiente = "pendiente";
    public const string EnCurso = "en_curso";
    public const string PorValidar = "por_validar";
    public const string Cerrada = "cerrada";
    public const string Cancelada = "cancelada";

    public static readonly string[] Abiertas = [Pendiente, EnCurso];
    public static readonly string[] Terminadas = [PorValidar, Cerrada, Cancelada];
}

public sealed record EstadoDeOrden(
    long Id, string Nombre, string Etiqueta, short Orden, string Fase, bool ExigeTecnico, string? CompletaFecha,
    string Tono, string? CodigoAlerta, bool EsInicial);

public sealed record TransicionDeEstado(
    string Origen, string Destino, bool ExigeMotivo, bool PuedeTecnico, string? Accion, bool Principal, bool AlAsignarTecnico);

/// <summary>
/// Las reglas de los estados de las órdenes tal como están en la base (<c>estado_ot</c> y <c>transicion_estado_ot</c>):
/// qué estados existen, en qué orden, a cuáles se puede pasar desde cada uno y qué exige cada cambio. El código no
/// nombra ningún estado: solo aplica estas reglas.
/// </summary>
public sealed class ReglasDeEstadoOrden
{
    private readonly Dictionary<string, EstadoDeOrden> _porNombre;
    private readonly Dictionary<string, List<TransicionDeEstado>> _desde;

    public ReglasDeEstadoOrden(IEnumerable<EstadoDeOrden> estados, IEnumerable<TransicionDeEstado> transiciones)
    {
        Estados = estados.OrderBy(e => e.Orden).ThenBy(e => e.Nombre, StringComparer.Ordinal).ToList();
        _porNombre = Estados.ToDictionary(e => e.Nombre);
        _desde = Estados.ToDictionary(e => e.Nombre, _ => new List<TransicionDeEstado>());
        foreach (var transicion in transiciones)
        {
            if (_desde.TryGetValue(transicion.Origen, out var salientes) && _porNombre.ContainsKey(transicion.Destino))
            {
                salientes.Add(transicion);
            }
        }

        foreach (var salientes in _desde.Values)
        {
            salientes.Sort((a, b) => _porNombre[a.Destino].Orden.CompareTo(_porNombre[b.Destino].Orden));
        }

        Inicial = Estados.FirstOrDefault(e => e.EsInicial)
            ?? throw new InvalidOperationException("El catálogo de estados de las órdenes no tiene un estado inicial.");
    }

    /// <summary>Los estados activos, en el orden en que se muestran.</summary>
    public IReadOnlyList<EstadoDeOrden> Estados { get; }

    /// <summary>El estado en el que nace una orden.</summary>
    public EstadoDeOrden Inicial { get; }

    public EstadoDeOrden? Buscar(string nombre) => _porNombre.GetValueOrDefault(nombre);

    public bool EsValido(string nombre) => _porNombre.ContainsKey(nombre);

    /// <summary>Transiciones que salen de un estado, ordenadas como el catálogo. Un estado desconocido no tiene ninguna.</summary>
    public IReadOnlyList<TransicionDeEstado> TransicionesDesde(string nombre) =>
        _desde.TryGetValue(nombre, out var salientes) ? salientes : [];

    public IReadOnlyList<string> Siguientes(string nombre) => TransicionesDesde(nombre).Select(t => t.Destino).ToList();

    /// <summary>Un estado final no tiene transiciones de salida: la orden ya no se modifica.</summary>
    public bool EsFinal(string nombre) => EsValido(nombre) && TransicionesDesde(nombre).Count == 0;

    public string? FaseDe(string nombre) => Buscar(nombre)?.Fase;

    public bool EstaCerrada(string nombre) => FaseDe(nombre) == FasesDeEstado.Cerrada;

    /// <summary>Estado al que pasa sola la orden cuando se le asigna un técnico, si el actual lo define.</summary>
    public string? EstadoAlAsignarTecnico(string estadoActual) =>
        TransicionesDesde(estadoActual).FirstOrDefault(t => t.AlAsignarTecnico)?.Destino;

    /// <summary>Qué fecha completa entrar en un estado: <c>inicio</c>, <c>cierre</c> o ninguna.</summary>
    public string? CompletaFecha(string nombre) => Buscar(nombre)?.CompletaFecha;

    /// <summary>
    /// Comprueba un cambio de estado: pasar al mismo estado no hace nada; si no hay transición, <c>TRANSICION_INVALIDA</c>;
    /// luego, si lo pide un técnico, que la transición sea de las que puede hacer (<c>TRANSICION_NO_PERMITIDA</c>);
    /// por último se exige técnico y motivo según el estado de destino y la transición.
    /// </summary>
    public void ValidarTransicion(string estadoActual, string estadoNuevo, long? tecnicoId, string? motivo, bool comoTecnico = false)
    {
        if (estadoNuevo == estadoActual)
        {
            return;
        }

        var transicion = TransicionesDesde(estadoActual).FirstOrDefault(t => t.Destino == estadoNuevo)
            ?? throw new ErrorDeOrdenException(ErrorDeOrdenException.TransicionInvalida);

        if (comoTecnico && !transicion.PuedeTecnico)
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.TransicionNoPermitida);
        }

        if (_porNombre[estadoNuevo].ExigeTecnico && tecnicoId is null)
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.TecnicoObligatorio);
        }

        if (transicion.ExigeMotivo && string.IsNullOrWhiteSpace(motivo))
        {
            throw new ErrorDeOrdenException(ErrorDeOrdenException.MotivoObligatorio);
        }
    }

    /// <summary>La etiqueta cargada en la base; si falta, el nombre con espacios y la primera letra en mayúscula.</summary>
    public static string EtiquetaDelEstado(string nombre, string? descripcion)
    {
        if (!string.IsNullOrWhiteSpace(descripcion))
        {
            return descripcion.Trim();
        }

        var legible = nombre.Replace('_', ' ').Trim();
        return legible.Length == 0 ? legible : char.ToUpperInvariant(legible[0]) + legible[1..];
    }
}
