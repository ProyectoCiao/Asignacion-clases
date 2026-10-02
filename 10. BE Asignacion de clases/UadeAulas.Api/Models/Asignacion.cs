namespace UadeAulas.Api.Models;

public class Asignacion
{
    public long Id { get; set; }
    public long ClaseId { get; set; }
    public int? AulaId { get; set; }
    public string Origen { get; set; } = string.Empty; // EXCEL, SUGERIDA, MANUAL
    public int SillasVacias { get; set; }

    public Clase? Clase { get; set; }
    public Aula? Aula { get; set; }
}
