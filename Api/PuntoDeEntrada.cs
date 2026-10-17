namespace Api;

/// <summary>
/// Marca el ensamblado de la API para que las pruebas de integración puedan iniciarla
/// con WebApplicationFactory sin tocar Program.cs (el tipo Program también existe en SOTRA-BACKEND).
/// </summary>
public sealed class PuntoDeEntrada;
