namespace UadeAulas.Api.Dtos;

/// <summary>
/// Una fila de la planificación guardada vista como clase. El aula efectiva es la editada (asignacion_aula) si existe;
/// si no, la "Aula Principal" del Excel cruzada con `aulas` por nombre. La demanda sale solo del Excel de demanda.
/// </summary>
public class ClaseDto
{
    public string Id { get; set; } = string.Empty;
    public string Numero { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public string Ciclo { get; set; } = string.Empty;
    public string Facultad { get; set; } = string.Empty;
    public string Sede { get; set; } = string.Empty;
    public string Turno { get; set; } = string.Empty;
    public string Horario { get; set; } = string.Empty;
    public string Dias { get; set; } = string.Empty;
    public string CodigoMateria { get; set; } = string.Empty;
    public string Materia { get; set; } = string.Empty;
    public string Docentes { get; set; } = string.Empty;

    /// <summary>Texto de "Aula Principal" en el Excel ("-" = sin aula).</summary>
    public string AulaExcel { get; set; } = string.Empty;
    public int? AulaId { get; set; }
    public string? Aula { get; set; }
    public int? Capacidad { get; set; }
    public int? SedeAulaId { get; set; }
    public int? EdificioId { get; set; }
    public string? Edificio { get; set; }
    public string? Piso { get; set; }
    public string? Tipo { get; set; }

    /// <summary>El aula efectiva difiere de la del Excel (hay una edición en asignacion_aula).</summary>
    public bool Modificada { get; set; }
    public string? ModificadaPor { get; set; }
    public DateTime? ModificadaEn { get; set; }

    /// <summary>Inscriptos en primera opción según el Excel de demanda (null si no hay demanda cargada o no trae "Id Clase").</summary>
    public int? Demanda { get; set; }
    /// <summary>Capacidad − demanda (negativo = sobrecupo). Null si falta alguno de los dos.</summary>
    public int? SillasVacias { get; set; }

    /// <summary>Alumnos asignados a la clase en primera opción (ausente si todavía no se corrió "Asignar alumnos a aulas").</summary>
    public int? Asignados1 { get; set; }
    /// <summary>Alumnos que la marcaron como segunda opción (tentativos).</summary>
    public int? Asignados2 { get; set; }
}

public class ResumenAsignacionAlumnosDto
{
    public int Filas { get; set; }
    public int Alumnos { get; set; }
    public int Opcion1 { get; set; }
    public int Opcion2 { get; set; }
    public DateTime? GeneradoEn { get; set; }
    public string? GeneradoPor { get; set; }
    /// <summary>Ids de clase de la demanda que no existen en la planificación (no se asignaron). Solo al generar.</summary>
    public int? IdsInexistentes { get; set; }
}

public class ClasesPaginaDto
{
    public bool Cargada { get; set; }
    public bool HayDemanda { get; set; }
    /// <summary>Ya se corrió "Asignar alumnos a aulas": Asignados1/2 de cada clase son válidos.</summary>
    public bool HayAsignaciones { get; set; }
    public int TotalFiltradas { get; set; }
    public int Pagina { get; set; }
    public int TamanioPagina { get; set; }
    public int TotalPaginas { get; set; }
    public List<ClaseDto> Clases { get; set; } = [];
}

public class CambiarAulaRequest
{
    /// <summary>Id de `aulas`. Null deja la clase sin aula.</summary>
    public int? AulaId { get; set; }

    /// <summary>Permite mover la clase a un aula con menos sillas que inscriptos (el choque de aula nunca se fuerza).</summary>
    public bool ForzarCupo { get; set; }
}

/// <summary>Motivo por el que no se puede (o no conviene) mover una clase a un aula. Tipo: CUPO, CHOQUE_AULA o CHOQUE_DOCENTE.</summary>
public class ConflictoDto
{
    public string Tipo { get; set; } = string.Empty;
    public string Detalle { get; set; } = string.Empty;
    /// <summary>Id de la otra clase involucrada (choques).</summary>
    public string? ClaseId { get; set; }
}

public class CambioAulaResultadoDto
{
    public ClaseDto Clase { get; set; } = new();
    /// <summary>Avisos que no impidieron el cambio (choques de docente, sobrecupo forzado).</summary>
    public List<ConflictoDto> Advertencias { get; set; } = [];
}

public class InscripcionRequest
{
    public string Legajo { get; set; } = string.Empty;
    public string Alumno { get; set; } = string.Empty;
    /// <summary>Id (del Excel de planificación) de la clase elegida en primera opción.</summary>
    public string ClaseId { get; set; } = string.Empty;
    /// <summary>Id de la clase en segunda opción (opcional).</summary>
    public string? ClaseId2 { get; set; }
}
