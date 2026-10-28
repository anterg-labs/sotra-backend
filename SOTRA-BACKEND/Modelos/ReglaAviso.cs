namespace SOTRA_BACKEND.Modelos;

/// <summary>Configuración de una regla de los avisos de ineficiencia (umbrales, ventanas y mínimo de datos).</summary>
public partial class ReglaAviso
{
    public string Codigo { get; set; } = null!;

    public string Nombre { get; set; } = null!;

    public bool Activa { get; set; }

    public decimal Umbral { get; set; }

    public decimal? UmbralCritico { get; set; }

    public int? VentanaDias { get; set; }

    public int? VentanaCriticaDias { get; set; }

    public int MinimoDatos { get; set; }
}
