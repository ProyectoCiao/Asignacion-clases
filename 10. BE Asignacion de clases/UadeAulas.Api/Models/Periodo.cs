namespace UadeAulas.Api.Models;

public class Periodo
{
    public int Id { get; set; }
    public string Ciclo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }
}
