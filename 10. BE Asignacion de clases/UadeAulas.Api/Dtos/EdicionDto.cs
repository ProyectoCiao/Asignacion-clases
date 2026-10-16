namespace UadeAulas.Api.Dtos;

/// <summary>
/// Lote de cambios sobre clases de un período. Se valida el estado final completo y se aplica todo junto
/// o nada: un intercambio de aulas (A↔B) o una rueda (A→B→C→A) son varios cambios en el mismo lote.
/// </summary>
public class LoteCambiosRequest
{
    public List<CambioEdicionDto> Cambios { get; set; } = new();
    /// <summary>Obligatorio si la planificación ya está congelada (cambio de emergencia).</summary>
    public string? Motivo { get; set; }
}

/// <summary>
/// Cambio sobre una clase. Los campos en null no se modifican.
/// No hay forma de quitarle el aula a una clase: solo se la puede mover a otra.
/// </summary>
public class CambioEdicionDto
{
    public long ClaseId { get; set; }
    public string? MateriaCodigo { get; set; }
    public List<int>? DocenteIds { get; set; }
    public string? Turno { get; set; }
    public List<string>? Dias { get; set; }
    /// <summary>"HH:mm"</summary>
    public string? HorarioDesde { get; set; }
    /// <summary>"HH:mm"</summary>
    public string? HorarioHasta { get; set; }
    public int? AulaId { get; set; }
}

public class LoteCambiosResponse
{
    public bool Exito { get; set; }
    public string? Error { get; set; }
    public List<string> Errores { get; set; } = new();
    /// <summary>No bloquean: ej. una clase queda con más inscriptos que la capacidad del aula.</summary>
    public List<string> Advertencias { get; set; } = new();
    public Guid? Lote { get; set; }
    public bool EsEmergencia { get; set; }
    public int ClasesModificadas { get; set; }
}

/// <summary>
/// Para la pantalla del backoffice: a qué aulas se puede mover una clase y qué implica cada una.
/// </summary>
public class OpcionesAulaResponse
{
    public long ClaseId { get; set; }
    public int NroClase { get; set; }
    public string Materia { get; set; } = string.Empty;
    public int Inscriptos { get; set; }
    public int? AulaActualId { get; set; }
    public string? AulaActualCodigo { get; set; }
    public int? AulaActualCapacidad { get; set; }
    /// <summary>Ordenadas: primero las que entran con menos sillas vacías.</summary>
    public List<OpcionAulaDto> Opciones { get; set; } = new();
}

public static class TipoOpcionAula
{
    /// <summary>El aula está libre en ese horario: alcanza con mover la clase.</summary>
    public const string Libre = "LIBRE";
    /// <summary>Hay una sola clase ocupándola y entra en el aula actual: intercambio A↔B.</summary>
    public const string IntercambioDirecto = "INTERCAMBIO_DIRECTO";
    /// <summary>Las ocupantes no pueden ir al aula actual: hay que reubicarlas en otras aulas (rueda).</summary>
    public const string RequiereRueda = "REQUIERE_RUEDA";
}

public class OpcionAulaDto
{
    public int AulaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string TipoGrupo { get; set; } = string.Empty;
    public int Piso { get; set; }
    public int Capacidad { get; set; }
    /// <summary>Capacidad − inscriptos de la clase. Negativo = no entran todos.</summary>
    public int SillasVacias { get; set; }
    public bool EntranTodos { get; set; }
    public string Tipo { get; set; } = TipoOpcionAula.Libre;
    /// <summary>Clases que ocupan el aula en un horario superpuesto con la clase.</summary>
    public List<OcupanteAulaDto> Ocupantes { get; set; } = new();
    /// <summary>Cambios listos para enviar a POST /periodos/{id}/cambios (solo LIBRE e INTERCAMBIO_DIRECTO).</summary>
    public List<CambioEdicionDto>? CambiosSugeridos { get; set; }
}

public class OcupanteAulaDto
{
    public long ClaseId { get; set; }
    public int NroClase { get; set; }
    public string Materia { get; set; } = string.Empty;
    public int Inscriptos { get; set; }
    public string[] Dias { get; set; } = Array.Empty<string>();
    public string Horario { get; set; } = string.Empty;
    /// <summary>Puede pasar al aula que deja la clase (entra y no choca con otra).</summary>
    public bool PuedeIrAlAulaActual { get; set; }
}
