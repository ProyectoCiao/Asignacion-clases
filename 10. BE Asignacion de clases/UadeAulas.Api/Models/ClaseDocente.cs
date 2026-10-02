namespace UadeAulas.Api.Models;

public class ClaseDocente
{
    public long ClaseId { get; set; }
    public int DocenteId { get; set; }

    public Clase? Clase { get; set; }
    public Docente? Docente { get; set; }
}
