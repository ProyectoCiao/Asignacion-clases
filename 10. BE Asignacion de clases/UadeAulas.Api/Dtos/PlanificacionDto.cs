using System.Text.Json.Serialization;

namespace UadeAulas.Api.Dtos;

public class CargaPlanificacionResultDto
{
    public bool Exito { get; set; }
    /// <summary>El período ya tiene planificación: el Excel no se vuelve a cargar (409).</summary>
    [JsonIgnore]
    public bool YaCargada { get; set; }
    public string? Error { get; set; }
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
    public string? Periodo { get; set; }
    public int FilasLeidas { get; set; }
    public int ClasesCreadas { get; set; }
    public int DocentesCreados { get; set; }
    public int AsignacionesCreadas { get; set; }
}

public class MotivoDto
{
    public string? Motivo { get; set; }
}

/// <summary>Copiar una planificación finalizada como punto de partida de otro cuatrimestre.</summary>
public class CopiarPlanificacionRequest
{
    public int Anio { get; set; }
    /// <summary>1 o 2</summary>
    public int Cuatrimestre { get; set; }
}

public class CopiaPlanificacionResultDto
{
    public bool Exito { get; set; }
    public string? Error { get; set; }
    public int? PeriodoId { get; set; }
    public string? Periodo { get; set; }
    public string? PeriodoOrigen { get; set; }
    public int ClasesCopiadas { get; set; }
    public int AsignacionesCopiadas { get; set; }
    public int DocentesCopiados { get; set; }
    /// <summary>Ej. clases que quedaron en aulas hoy inactivas: hay que moverlas.</summary>
    public List<string> Advertencias { get; set; } = new();
}

/// <summary>
/// Fechas del ciclo de la planificación que carga el backoffice. Se envían con zona horaria
/// (ej. "2026-11-02T09:00:00-03:00"). Null = todavía sin definir.
/// Si la planificación ya está congelada, el cambio es de emergencia y Motivo es obligatorio.
/// </summary>
public class FechasPlanificacionDto
{
    public DateTimeOffset? FechaCongelamiento { get; set; }
    public DateTimeOffset? FechaFinInscripcion { get; set; }
    public string? Motivo { get; set; }
}
