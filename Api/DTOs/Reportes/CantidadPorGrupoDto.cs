namespace Api.DTOs.Reportes;

public class CantidadPorGrupoDto
{
    /// <summary>Nombre.</summary>
    public required string Nombre { get; init; }
    /// <summary>Cantidad (unidades, órdenes u otro conteo según el contexto).</summary>
    public int Cantidad { get; set; }
}
