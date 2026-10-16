namespace UadeAulas.Api.Dtos;

/// <summary>
/// Aula de la tabla `aulas` con sus vínculos resueltos: sede (sedes), edificio (edificios) y tipo (aula_tipo).
/// Los vínculos son opcionales: hay aulas sin sede, sin edificio o sin tipo cargado.
/// </summary>
public class AulaMaestroDto
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int? Capacidad { get; set; }
    public string? Piso { get; set; }
    public int? SedeId { get; set; }
    public string? Sede { get; set; }
    public int? EdificioId { get; set; }
    public string? Edificio { get; set; }
    public int? TipoId { get; set; }
    public string? Tipo { get; set; }
}
