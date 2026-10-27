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

    /// <summary>
    /// Ajuste de Weibull por rango mediano (aproximación de Bernard): regresión de ln(-ln(1-F)) contra ln(t).
    /// La forma es la pendiente; la escala, el tiempo en que falla el 63,2 %. Devuelve <c>null</c> si no se puede ajustar.
    /// </summary>
    public static (double Forma, double Escala, double R2)? AjusteWeibull(IReadOnlyList<double> tiempos)
    {
        var orden = tiempos.Where(tiempo => tiempo > 0).OrderBy(tiempo => tiempo).ToList();
        var n = orden.Count;
        if (n < 3)
        {
            return null;
        }

        var x = new double[n];
        var y = new double[n];
        for (var i = 0; i < n; i++)
        {
            var f = (i + 1 - 0.3) / (n + 0.4);
            x[i] = Math.Log(orden[i]);
            y[i] = Math.Log(-Math.Log(1 - f));
        }

        var mediaX = x.Average();
        var mediaY = y.Average();
        double sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < n; i++)
        {
            sxy += (x[i] - mediaX) * (y[i] - mediaY);
            sxx += (x[i] - mediaX) * (x[i] - mediaX);
            syy += (y[i] - mediaY) * (y[i] - mediaY);
        }

        if (sxx == 0 || syy == 0)
        {
            return null;
        }

        var forma = sxy / sxx;
        if (forma <= 0)
        {
            return null;
        }

        var ordenada = mediaY - forma * mediaX;
        var escala = Math.Exp(-ordenada / forma);
        var r2 = sxy * sxy / (sxx * syy);
        return (forma, escala, r2);
    }
}
