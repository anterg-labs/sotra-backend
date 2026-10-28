namespace Api.Servicios;

/// <summary>Cálculos estadísticos simples y explicables para el análisis de desgaste.</summary>
public static class Estadistica
{
    /// <summary>Pendiente de la recta que mejor ajusta los valores en el orden dado (cambio por posición).</summary>
    public static double Pendiente(IReadOnlyList<double> valores)
    {
        var n = valores.Count;
        if (n < 2)
        {
            return 0;
        }

        var mediaX = (n - 1) / 2.0;
        var mediaY = valores.Average();
        double numerador = 0, denominador = 0;
        for (var i = 0; i < n; i++)
        {
            numerador += (i - mediaX) * (valores[i] - mediaY);
            denominador += (i - mediaX) * (i - mediaX);
        }

        return denominador == 0 ? 0 : numerador / denominador;
    }

}
