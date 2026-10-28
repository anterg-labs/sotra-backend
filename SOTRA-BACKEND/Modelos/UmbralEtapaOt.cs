namespace SOTRA_BACKEND.Modelos;

/// <summary>Referencia de duración por etapa; null utiliza la media histórica de etapas terminadas.</summary>
public class UmbralEtapaOt
{
    public string Etapa { get; set; } = null!;
    public double? UmbralHoras { get; set; }
}
