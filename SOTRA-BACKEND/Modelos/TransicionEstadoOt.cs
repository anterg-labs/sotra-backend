namespace SOTRA_BACKEND.Modelos;

public partial class TransicionEstadoOt
{
    public long IdEstadoOrigen { get; set; }

    public long IdEstadoDestino { get; set; }

    public bool ExigeMotivo { get; set; }

    public bool PuedeTecnico { get; set; }

    public string? Accion { get; set; }

    public bool Principal { get; set; }

    public bool AlAsignarTecnico { get; set; }

    public virtual EstadoOt IdEstadoOrigenNavigation { get; set; } = null!;

    public virtual EstadoOt IdEstadoDestinoNavigation { get; set; } = null!;
}
