namespace Api.DTOs.Clientes;

public class ClienteListadoDto
{
    public long Id { get; init; }
    public required string RazonSocial { get; init; }
    public required string NombreContacto { get; init; }
    public string? Correo { get; init; }
    public string? Telefono { get; init; }
    public string? Direccion { get; init; }
    public string? Observaciones { get; init; }
    public int CantidadActivosAsociados { get; init; }
    public int CantidadOrdenesAsociadas { get; init; }
    public required string Estado { get; init; }
    public DateOnly CreadoEn { get; init; }
}
