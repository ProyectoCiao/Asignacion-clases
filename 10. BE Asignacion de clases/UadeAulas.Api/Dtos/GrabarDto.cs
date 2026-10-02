namespace UadeAulas.Api.Dtos;

public class GrabarRequest
{
    public string Confirmacion { get; set; } = string.Empty;
    public List<CambioClaseDto> Cambios { get; set; } = new();
}

public class CambioClaseDto
{
    public long Id { get; set; }
    public int? AulaId { get; set; }
    public int InscriptosConReserva { get; set; }
    public string Turno { get; set; } = string.Empty;
}

public class ChoqueDto
{
    public long ClaseId { get; set; }
    public int NroClase { get; set; }
    public string Materia { get; set; } = string.Empty;
    public string AulaCodigo { get; set; } = string.Empty;
    public string Horario { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

public class SobrecupoDto
{
    public long ClaseId { get; set; }
    public int NroClase { get; set; }
    public string Materia { get; set; } = string.Empty;
    public int Inscriptos { get; set; }
    public int CapacidadAula { get; set; }
    public int Exceso { get; set; }
}

public class GrabarResponse
{
    public List<OfertaRowDto> Filas { get; set; } = new();
    public List<ChoqueDto> Choques { get; set; } = new();
    public List<SobrecupoDto> Sobrecupo { get; set; } = new();
}
