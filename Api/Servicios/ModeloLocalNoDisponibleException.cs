namespace Api.Servicios;

/// <summary>
/// Falta un modelo de IA que corre dentro de la API (embeddings o reranker): no están las variables de entorno o los
/// archivos. Es un problema de instalación, no un fallo pasajero.
/// </summary>
public class ModeloLocalNoDisponibleException : Exception
{
    public ModeloLocalNoDisponibleException(string mensaje) : base(mensaje)
    {
    }
}
