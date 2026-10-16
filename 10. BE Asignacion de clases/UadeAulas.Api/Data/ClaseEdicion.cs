namespace UadeAulas.Api.Data;

/// <summary>
/// Estado final de una clase ya validado, para aplicar en la base.
/// DocenteIds / AulaId en null = no se tocan.
/// </summary>
public class ClaseEdicion
{
    public long ClaseId { get; set; }
    public string MateriaCodigo { get; set; } = string.Empty;
    public string Turno { get; set; } = string.Empty;
    public string[] Dias { get; set; } = Array.Empty<string>();
    public TimeOnly HorarioDesde { get; set; }
    public TimeOnly HorarioHasta { get; set; }
    public List<int>? DocenteIds { get; set; }
    public int? AulaId { get; set; }
    public int SillasVacias { get; set; }
}
