namespace UadeAulas.Api.Dtos;

public class DemandaRowDto
{
    public string MateriaCodigo { get; set; } = string.Empty;
    public string MateriaNombre { get; set; } = string.Empty;
    public string? Facultad { get; set; }
    public int Solicitudes { get; set; }
    public int NumClases { get; set; }
    public int Asientos { get; set; }
    public int Brecha { get; set; }
}

public class ActualizarDemandaResponse
{
    public List<DemandaRowDto> Demandas { get; set; } = new();
}
