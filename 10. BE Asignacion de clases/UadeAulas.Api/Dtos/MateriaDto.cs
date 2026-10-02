namespace UadeAulas.Api.Dtos;

public class MateriaDto
{
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Facultad { get; set; }
    public string? Departamento { get; set; }
    public int CargaHoraria { get; set; }
}
