namespace SOTRA_BACKEND.Modelos;

public partial class ValidacionRecomendacionIa
{
    public long IdValidacion { get; set; }

    public long IdRecomendacionIa { get; set; }

    public long? IdUsuario { get; set; }

    public string Resultado { get; set; } = null!;

    public string? Observacion { get; set; }

    public DateTime Fecha { get; set; }
}
