namespace UadeAulas.Api.Data;

public class CopiaPlanificacionResultado
{
    public int PeriodoId { get; set; }
    public int Clases { get; set; }
    public int Asignaciones { get; set; }
    /// <summary>Filas de clase_docente copiadas.</summary>
    public int Docentes { get; set; }
    public List<string> ClasesEnAulasInactivas { get; set; } = new();
}
