using System.Globalization;
using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;

namespace UadeAulas.Api.Services;

public class SinFiltrosException() : Exception("Aplicá al menos un filtro para ver los totales.");

/// <summary>
/// Demanda provisoria: Excel subido por el backoffice y guardado tal cual. No lista alumno por alumno: solo totales
/// (inscripciones = filas, alumnos = legajos distintos) por la columna que se elija, siempre con algún filtro aplicado.
/// Cuando llegue la API de demanda, la idea es reemplazar solo el origen de las filas.
/// </summary>
public class DemandaExcelService
{
    public const string ValorVacio = "(vacío)";
    private const int MaxValoresOpciones = 5000;

    // Las filas se parsean una sola vez por Excel subido (el jsonb puede ser grande): se reutilizan mientras el id
    // vigente en la base no cambie.
    private static readonly object CacheLock = new();
    private static DemandaExcelGuardada? _cache;

    private readonly DemandaExcelRepository _repository;

    public DemandaExcelService(DemandaExcelRepository repository)
    {
        _repository = repository;
    }

    public async Task<int> GuardarAsync(string archivo, string usuario, ExcelCrudo excel)
    {
        await _repository.ReemplazarAsync(archivo, usuario, excel.Encabezados, excel.Filas);
        return excel.Filas.Count;
    }

    public async Task<DemandaExcelInfoDto> GetInfoAsync()
    {
        var datos = await ObtenerAsync();
        if (datos == null) return new DemandaExcelInfoDto { Cargada = false };

        return new DemandaExcelInfoDto
        {
            Cargada = true,
            Archivo = datos.NombreArchivo,
            SubidoEn = datos.SubidoEn,
            SubidoPor = datos.SubidoPor,
            Encabezados = datos.Encabezados,
            TotalFilas = datos.Filas.Count,
            ColumnaLegajo = ColumnaLegajo(datos.Encabezados),
        };
    }

    /// <summary>Valores posibles de una columna dentro de lo que dejan pasar los demás filtros (filtros encadenados).</summary>
    public async Task<List<string>?> GetValoresAsync(ValoresColumnaRequest request)
    {
        var datos = await ObtenerAsync();
        if (datos == null) return null;
        ValidarColumna(datos, request.Columna);

        var otrosFiltros = request.Filtros.Where(f => f.Columna != request.Columna).ToList();
        return Filtrar(datos, otrosFiltros)
            .Select(f => Celda(f, request.Columna))
            .GroupBy(ExcelCrudoService.Normalizar)
            .Select(g => g.First())
            .OrderBy(v => v, StringComparer.Create(new CultureInfo("es-AR"), true))
            .Take(MaxValoresOpciones)
            .ToList();
    }

    public async Task<TotalesDemandaDto?> GetTotalesAsync(TotalesDemandaRequest request)
    {
        var datos = await ObtenerAsync();
        if (datos == null) return null;

        var filtros = request.Filtros.Where(f => !string.IsNullOrWhiteSpace(f.Valor)).ToList();
        if (filtros.Count == 0) throw new SinFiltrosException();
        foreach (var f in filtros) ValidarColumna(datos, f.Columna);
        ValidarColumna(datos, request.AgruparPor);

        var colLegajo = ColumnaLegajo(datos.Encabezados);
        var filas = Filtrar(datos, filtros).ToList();

        var grupos = filas
            .GroupBy(f => ExcelCrudoService.Normalizar(Celda(f, request.AgruparPor)))
            .Select(g => new TotalGrupoDto
            {
                Valor = Celda(g.First(), request.AgruparPor),
                Inscripciones = g.Count(),
                Alumnos = colLegajo == null ? null : g.Select(f => f[colLegajo.Value]).Where(v => v.Length > 0).Distinct().Count(),
            });

        var comparador = StringComparer.Create(new CultureInfo("es-AR"), true);
        grupos = (request.OrdenarPor ?? "inscripciones").ToLowerInvariant() switch
        {
            "valor" => request.Descendente ? grupos.OrderByDescending(g => g.Valor, comparador) : grupos.OrderBy(g => g.Valor, comparador),
            "alumnos" => request.Descendente ? grupos.OrderByDescending(g => g.Alumnos) : grupos.OrderBy(g => g.Alumnos),
            _ => request.Descendente ? grupos.OrderByDescending(g => g.Inscripciones) : grupos.OrderBy(g => g.Inscripciones),
        };
        var lista = grupos.ToList();

        var tamanio = Math.Clamp(request.Tamanio, 1, ExcelCrudoService.MaxFilasPorPagina);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(lista.Count / (double)tamanio));
        var pagina = Math.Clamp(request.Pagina, 1, totalPaginas);

        return new TotalesDemandaDto
        {
            Columna = datos.Encabezados[request.AgruparPor],
            InscripcionesFiltradas = filas.Count,
            AlumnosFiltrados = colLegajo == null ? null : filas.Select(f => f[colLegajo.Value]).Where(v => v.Length > 0).Distinct().Count(),
            TotalGrupos = lista.Count,
            Pagina = pagina,
            TamanioPagina = tamanio,
            TotalPaginas = totalPaginas,
            Grupos = lista.Skip((pagina - 1) * tamanio).Take(tamanio).ToList(),
        };
    }

    /// <summary>Distintas columnas se combinan con Y; varios valores de la misma columna, con O.</summary>
    private static IEnumerable<List<string>> Filtrar(DemandaExcelGuardada datos, List<FiltroDemandaDto> filtros)
    {
        var porColumna = filtros
            .Where(f => !string.IsNullOrWhiteSpace(f.Valor))
            .GroupBy(f => f.Columna)
            .Select(g => (Columna: g.Key, Valores: g.Select(f => ExcelCrudoService.Normalizar(f.Valor)).ToHashSet()))
            .ToList();

        return datos.Filas.Where(fila =>
            porColumna.All(f => f.Valores.Contains(ExcelCrudoService.Normalizar(Celda(fila, f.Columna)))));
    }

    private static string Celda(List<string> fila, int columna)
    {
        var valor = columna < fila.Count ? fila[columna] : string.Empty;
        return valor.Length > 0 ? valor : ValorVacio;
    }

    private static int? ColumnaLegajo(List<string> encabezados)
    {
        var idx = encabezados.FindIndex(e => ExcelCrudoService.Normalizar(e).Contains("legajo"));
        return idx >= 0 ? idx : null;
    }

    private static void ValidarColumna(DemandaExcelGuardada datos, int columna)
    {
        if (columna < 0 || columna >= datos.Encabezados.Count)
            throw new ArgumentOutOfRangeException(nameof(columna), $"La columna {columna} no existe en el Excel de demanda.");
    }

    /// <summary>Agrega una inscripción al Excel guardado. Falso si no hay Excel de demanda cargado.</summary>
    public async Task<bool> AgregarFilaAsync(List<string> fila)
    {
        var ok = await _repository.AgregarFilaAsync(fila);
        // El id no cambia al agregar una fila: hay que descartar la copia en memoria a mano
        lock (CacheLock) _cache = null;
        return ok;
    }

    public Task<DemandaExcelGuardada?> ObtenerDatosAsync() => ObtenerAsync();

    private async Task<DemandaExcelGuardada?> ObtenerAsync()
    {
        var idVigente = await _repository.GetIdVigenteAsync();
        if (idVigente == null) return null;

        lock (CacheLock)
            if (_cache?.Id == idVigente) return _cache;

        var datos = await _repository.ObtenerAsync();
        lock (CacheLock) _cache = datos;
        return datos;
    }
}
