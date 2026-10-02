namespace UadeAulas.Api.Dtos;

public class CargaPlanificacionResultDto
{
    public bool Exito { get; set; }
    public string? Error { get; set; }
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
    public string? Periodo { get; set; }
    public int FilasLeidas { get; set; }
    public int ClasesCreadas { get; set; }
    public int DocentesCreados { get; set; }
    public int AsignacionesCreadas { get; set; }
}
