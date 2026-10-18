namespace SOTRA_BACKEND.Operaciones;

/// <summary>Regla única del estado de stock: sin stock con cantidad 0; bajo si hay un mínimo definido y la cantidad lo alcanza.</summary>
public static class EstadosStock
{
    public const string Normal = "normal";
    public const string Bajo = "bajo";
    public const string SinStock = "sin_stock";

    public static readonly IReadOnlyList<string> Validos = [Normal, Bajo, SinStock];

    public static string Calcular(int cantidad, int? cantidadMinima)
    {
        if (cantidad <= 0)
        {
            return SinStock;
        }

        var minimo = cantidadMinima ?? 0;
        return minimo > 0 && cantidad <= minimo ? Bajo : Normal;
    }
}

public sealed record AlertasStock(int Bajo, int SinStock)
{
    public int Total => Bajo + SinStock;
}
