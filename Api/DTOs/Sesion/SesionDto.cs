namespace Api.DTOs.Sesion;

public class SesionDto
{
    public required string IdKeycloak { get; init; }
    public string? Correo { get; init; }
    public string? Nombre { get; init; }
    public required List<string> Roles { get; init; }
}
