namespace UadeAulas.Api.Dtos;

/// <summary>Columnas que la planificación va a necesitar a futuro y que el Excel no trae (solo advierten).</summary>
public class ColumnasFaltantesDto
{
    public List<string> Obligatorias { get; set; } = new();
    public List<string> Opcionales { get; set; } = new();
}

/// <summary>Resultado de analizar un Excel sin guardarlo (paso previo a la subida).</summary>
public class ExcelLeidoDto
{
    public string Archivo { get; set; } = string.Empty;
    public List<string> Encabezados { get; set; } = new();
    public int TotalFilas { get; set; }
    public ColumnasFaltantesDto? Faltantes { get; set; }
}

public class OpcionesFiltroDto
{
    public List<string> Sedes { get; set; } = new();
    public List<string> Facultades { get; set; } = new();
}

public class PlanificacionInicialPaginaDto
{
    public bool Cargada { get; set; }
    public string? Archivo { get; set; }
    public DateTime? SubidoEn { get; set; }
    public string? SubidoPor { get; set; }
    public int? Anio { get; set; }
    public int? Cuatrimestre { get; set; }
    public List<string>? Encabezados { get; set; }
    public ColumnasFaltantesDto? Faltantes { get; set; }
    public int TotalFilas { get; set; }
    public int TotalFiltradas { get; set; }
    public int Pagina { get; set; }
    public int TamanioPagina { get; set; }
    public int TotalPaginas { get; set; }
    public List<List<string>>? Filas { get; set; }
    public OpcionesFiltroDto? Opciones { get; set; }
}
