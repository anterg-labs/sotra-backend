namespace Api.DTOs.OrdenesTrabajo;

public class SolicitudSugerenciaDto
{
    public string Descripcion { get; init; } = "";
    public int Numero { get; init; } = 1;
    public Guid? Sesion { get; init; }
}
