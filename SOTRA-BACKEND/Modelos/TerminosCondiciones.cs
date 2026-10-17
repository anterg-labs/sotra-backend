namespace SOTRA_BACKEND.Modelos;

public partial class TerminosCondiciones
{
    public long IdTerminos { get; set; }

    public int Version { get; set; }

    public string Titulo { get; set; } = null!;

    public string Texto { get; set; } = null!;

    /// <summary>Datos que se guardan de las personas usuarias, uno por línea.</summary>
    public string DatosAlmacenados { get; set; } = null!;

    public bool Vigente { get; set; }

    public DateTime CreadoEn { get; set; }

    public long? IdUsuarioAutor { get; set; }
}

public partial class AceptacionTerminos
{
    public long IdAceptacion { get; set; }

    public long IdUsuario { get; set; }

    public long IdTerminos { get; set; }

    public DateTime AceptadoEn { get; set; }
}
