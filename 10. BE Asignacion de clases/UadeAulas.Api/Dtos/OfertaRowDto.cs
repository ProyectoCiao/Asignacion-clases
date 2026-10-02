namespace UadeAulas.Api.Dtos;

public class OfertaRowDto
{
    public long ClaseId { get; set; }
    public int NroClase { get; set; }
    public string MateriaCodigo { get; set; } = string.Empty;
    public string MateriaNombre { get; set; } = string.Empty;
    public string? Facultad { get; set; }
    public string? Departamento { get; set; }
    public int SedeId { get; set; }
    public string SedeCodigo { get; set; } = string.Empty;
    public string SedeNombre { get; set; } = string.Empty;
    public int? AulaId { get; set; }
    public string? AulaCodigo { get; set; }
    public string? AulaTipo { get; set; }
    public int? AulaCapacidad { get; set; }
    public int? AulaPiso { get; set; }
    public string Turno { get; set; } = string.Empty;
    public string HorarioDesde { get; set; } = string.Empty;
    public string HorarioHasta { get; set; } = string.Empty;
    public string[]? Dias { get; set; }
    public string? Regimen { get; set; }
    public string? Idioma { get; set; }
    public string? Modalidad { get; set; }
    public int Inscriptos { get; set; }
    public int InscriptosConReserva { get; set; }
    public string Docentes { get; set; } = string.Empty;
    public int SillasVacias { get; set; }
    public string Origen { get; set; } = string.Empty;
}
