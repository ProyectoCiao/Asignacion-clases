namespace UadeAulas.Api.Data;

/// <summary>Fila de planificación ya validada y resuelta, lista para insertar en la base.</summary>
public class PlanificacionFilaInsert
{
    public int SedeId { get; set; }
    public string MateriaCodigo { get; set; } = string.Empty;
    public string Turno { get; set; } = string.Empty;
    public string[] Dias { get; set; } = Array.Empty<string>();
    public TimeOnly HorarioDesde { get; set; }
    public TimeOnly HorarioHasta { get; set; }
    public string? Regimen { get; set; }
    public string? Idioma { get; set; }
    public int Inscriptos { get; set; }
    public int? AulaId { get; set; }
    public int? AulaCapacidadTotal { get; set; }
    public List<int> DocenteIds { get; set; } = new();
}
