namespace UadeAulas.Api.Dtos;

public class CatalogosDto
{
    public List<SedeDto> Sedes { get; set; } = new();
    public List<string> Facultades { get; set; } = new();
    public List<AulaDto> Aulas { get; set; } = new();
}
