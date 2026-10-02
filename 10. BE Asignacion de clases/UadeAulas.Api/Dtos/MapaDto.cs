namespace UadeAulas.Api.Dtos;

public class MapaDto
{
    public int AulaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string TipoGrupo { get; set; } = string.Empty;
    public int CapacidadTotal { get; set; }
    public int Ocupados { get; set; }
    public int? ClaseNro { get; set; }
    public string? Materia { get; set; }
    public string? Horario { get; set; }
    public string? Docentes { get; set; }
}

public class CrearClaseRequest
{
    public int SedeId { get; set; }
    public string MateriaCodigo { get; set; } = string.Empty;
    public string Turno { get; set; } = string.Empty;
    public string[] Dias { get; set; } = Array.Empty<string>();
    public string HorarioDesde { get; set; } = string.Empty;
    public string HorarioHasta { get; set; } = string.Empty;
    public int Inscriptos { get; set; }
    public int? AulaId { get; set; }
}

public class CrearClaseResponse
{
    public long ClaseId { get; set; }
    public int NroClase { get; set; }
    public string Mensaje { get; set; } = string.Empty;
}
