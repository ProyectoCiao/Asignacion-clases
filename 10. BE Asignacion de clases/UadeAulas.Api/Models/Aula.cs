namespace UadeAulas.Api.Models;

public class Aula
{
    public int Id { get; set; }
    public int SedeId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string TipoGrupo { get; set; } = string.Empty; // AULA, HIBRIDA, LAB, TALLER, ARTE, REMOTA, SIN_AULA
    public string? TipoDetalle { get; set; }
    public int Piso { get; set; }
    public int CapacidadReal { get; set; }
    public int CapacidadTotal { get; set; }
    public string[]? Equipamiento { get; set; }
    public bool Activa { get; set; }
    public string? Edificio { get; set; }
    public string? PisoTexto { get; set; } // Piso tal cual el Excel ("S1", "Lab", "B"...); Piso es el entero derivado
    public string? NumeroAula { get; set; }

    public Sede? Sede { get; set; }
}
