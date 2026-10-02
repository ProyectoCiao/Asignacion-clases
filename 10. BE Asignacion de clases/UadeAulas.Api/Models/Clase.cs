namespace UadeAulas.Api.Models;

public class Clase
{
    public long Id { get; set; }
    public int NroClase { get; set; }
    public int PeriodoId { get; set; }
    public int SedeId { get; set; }
    public string MateriaCodigo { get; set; } = string.Empty;
    public string Turno { get; set; } = string.Empty; // MANANA, TARDE, NOCHE
    public TimeOnly HorarioDesde { get; set; }
    public TimeOnly HorarioHasta { get; set; }
    public string[]? Dias { get; set; }
    public string? Regimen { get; set; } // REMOTO, PRESENCIAL, etc.
    public string? Idioma { get; set; }
    public string? Modalidad { get; set; }
    public int Inscriptos { get; set; }
    public int InscriptosConReserva { get; set; }
    public string[]? Packs { get; set; }

    public Periodo? Periodo { get; set; }
    public Sede? Sede { get; set; }
    public Materia? Materia { get; set; }
}
