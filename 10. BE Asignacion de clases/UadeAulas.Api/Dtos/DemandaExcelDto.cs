namespace UadeAulas.Api.Dtos;

public class DemandaExcelInfoDto
{
    public bool Cargada { get; set; }
    public string? Archivo { get; set; }
    public DateTime? SubidoEn { get; set; }
    public string? SubidoPor { get; set; }
    public List<string> Encabezados { get; set; } = new();
    public int TotalFilas { get; set; }
    /// <summary>Índice de la columna de legajo (para contar alumnos distintos); null si el Excel no la trae.</summary>
    public int? ColumnaLegajo { get; set; }
}

/// <summary>Filtro sobre una columna (por índice). Varios valores de la misma columna se combinan con O.</summary>
public class FiltroDemandaDto
{
    public int Columna { get; set; }
    public string Valor { get; set; } = string.Empty;
}

public class TotalesDemandaRequest
{
    public List<FiltroDemandaDto> Filtros { get; set; } = new();
    public int AgruparPor { get; set; }
    /// <summary>"valor", "inscripciones" o "alumnos".</summary>
    public string? OrdenarPor { get; set; }
    public bool Descendente { get; set; } = true;
    public int Pagina { get; set; } = 1;
    public int Tamanio { get; set; } = 200;
}

public class ValoresColumnaRequest
{
    public List<FiltroDemandaDto> Filtros { get; set; } = new();
    public int Columna { get; set; }
}

public class TotalGrupoDto
{
    public string Valor { get; set; } = string.Empty;
    public int Inscripciones { get; set; }
    public int? Alumnos { get; set; }
}

public class TotalesDemandaDto
{
    public string Columna { get; set; } = string.Empty;
    public int InscripcionesFiltradas { get; set; }
    public int? AlumnosFiltrados { get; set; }
    public int TotalGrupos { get; set; }
    public int Pagina { get; set; }
    public int TamanioPagina { get; set; }
    public int TotalPaginas { get; set; }
    public List<TotalGrupoDto> Grupos { get; set; } = new();
}
