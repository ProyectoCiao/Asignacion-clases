namespace UadeAulas.Api.Dtos;

public class SugerirRequest
{
    public long ClaseId { get; set; }
}

public class AulaAltDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string TipoGrupo { get; set; } = string.Empty;
    public int CapacidadTotal { get; set; }
    public int Piso { get; set; }
    public int SillasVacias { get; set; }
    public int Puntaje { get; set; }
}

public class AulaBloqueadaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Motivo { get; set; } = string.Empty;
}

public class SugerirResponse
{
    public long ClaseId { get; set; }
    public int NroClase { get; set; }
    public string Materia { get; set; } = string.Empty;
    public int Inscriptos { get; set; }
    public AulaAltDto? AulaActual { get; set; }
    public string Regimen { get; set; } = string.Empty;
    public AulaAltDto? Recomendada { get; set; }
    public List<AulaAltDto> Alternativas { get; set; } = new();
    public List<AulaBloqueadaDto> Bloqueadas { get; set; } = new();
    public bool YaOptima { get; set; }
}
