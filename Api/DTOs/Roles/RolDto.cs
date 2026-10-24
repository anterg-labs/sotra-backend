namespace Api.DTOs.Roles;

public class RolDto
{
    public long Id { get; init; }
    public required string Nombre { get; init; }
    public required string Estado { get; init; }
}
