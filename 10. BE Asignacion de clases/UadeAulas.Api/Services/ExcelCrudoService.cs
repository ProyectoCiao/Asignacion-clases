using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;

namespace UadeAulas.Api.Services;

/// <summary>Excel leído tal cual: encabezados de la primera fila usada y el resto como texto.</summary>
public record ExcelCrudo(List<string> Encabezados, List<List<string>> Filas);

public class ExcelInvalidoException(string mensaje) : Exception(mensaje);

/// <summary>
/// Planificación inicial (se sube una sola vez, sin reglas: solo se advierten las columnas que faltan)
/// y lectura libre de cualquier Excel (Demanda, por ahora sin guardar).
/// </summary>
public class ExcelCrudoService
{
    public const int MaxFilasPorPagina = 200;

    private readonly PlanificacionExcelRepository _repository;

    public ExcelCrudoService(PlanificacionExcelRepository repository)
    {
        _repository = repository;
    }

    // Columnas que la planificación va a necesitar a futuro. Se reconocen igual que en la carga anterior:
    // sin importar mayúsculas, acentos ni guiones bajos, y también si el encabezado contiene el texto.
    private static readonly (string Dato, string[] Candidatos)[] Obligatorias =
    [
        ("Materia (código)", ["materia codigo", "codigo materia", "materia"]),
        ("Sede", ["sede codigo", "sede id", "sede"]),
        ("Turno", ["turno"]),
        ("Día", ["dias", "dia"]),
    ];

    private static readonly (string Dato, string[] Candidatos)[] Opcionales =
    [
        ("Aula", ["aula codigo", "aula id", "aula"]),
        ("Inscriptos", ["inscriptos"]),
        ("Docente(s)", ["docente s", "docentes", "docente"]),
        ("Régimen", ["regimen"]),
        ("Idioma", ["idioma"]),
    ];

    public static ExcelCrudo Leer(Stream stream)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex)
        {
            throw new ExcelInvalidoException($"No se pudo leer el archivo Excel: {ex.Message}");
        }

        using (workbook)
        {
            var hoja = workbook.Worksheets.FirstOrDefault()
                ?? throw new ExcelInvalidoException("El archivo Excel no tiene hojas.");
            var filaEncabezado = hoja.FirstRowUsed()
                ?? throw new ExcelInvalidoException("El archivo Excel está vacío.");

            var primeraCol = hoja.FirstColumnUsed()!.ColumnNumber();
            var ultimaCol = hoja.LastColumnUsed()!.ColumnNumber();

            var encabezados = new List<string>();
            for (var c = primeraCol; c <= ultimaCol; c++)
            {
                var texto = filaEncabezado.Cell(c).GetFormattedString().Trim();
                encabezados.Add(texto.Length > 0 ? texto : $"Columna {c - primeraCol + 1}");
            }

            var filas = new List<List<string>>();
            foreach (var fila in hoja.RowsUsed().Where(r => r.RowNumber() > filaEncabezado.RowNumber()))
            {
                var valores = new List<string>(encabezados.Count);
                for (var c = primeraCol; c <= ultimaCol; c++)
                    valores.Add(fila.Cell(c).GetFormattedString().Trim());
                if (valores.Any(v => v.Length > 0))
                    filas.Add(valores);
            }

            return new ExcelCrudo(encabezados, filas);
        }
    }

    public static ColumnasFaltantesDto ColumnasFaltantes(List<string> encabezados)
    {
        var normalizados = encabezados.Select(Normalizar).ToList();
        var faltantes = new ColumnasFaltantesDto
        {
            Obligatorias = Obligatorias.Where(o => BuscarColumna(normalizados, o.Candidatos) == null).Select(o => o.Dato).ToList(),
            Opcionales = Opcionales.Where(o => BuscarColumna(normalizados, o.Candidatos) == null).Select(o => o.Dato).ToList(),
        };

        // Horario: dos columnas (desde y hasta) o una sola con el rango
        var separado = BuscarColumna(normalizados, ["horario desde", "hora desde", "desde"]) != null
                    && BuscarColumna(normalizados, ["horario hasta", "hora hasta", "hasta"]) != null;
        if (!separado && BuscarColumna(normalizados, ["horario"]) == null)
            faltantes.Obligatorias.Add("Horario desde/hasta");

        return faltantes;
    }

    public async Task<int> GuardarPlanificacionAsync(
        string nombreArchivo, string usuario, int? anio, int? cuatrimestre, ExcelCrudo excel)
    {
        await _repository.ReemplazarAsync(nombreArchivo, usuario, anio, cuatrimestre, excel.Encabezados, excel.Filas);
        return excel.Filas.Count;
    }

    public Task<bool> HayPlanificacionAsync() => _repository.ExisteAsync();

    public async Task<PlanificacionInicialPaginaDto> GetPaginaAsync(
        int pagina, int tamanio, string[]? sedes, string[]? turnos, string[]? facultades, string? q)
    {
        var guardada = await _repository.ObtenerAsync();
        if (guardada == null)
            return new PlanificacionInicialPaginaDto { Cargada = false };

        var normalizados = guardada.Encabezados.Select(Normalizar).ToList();
        var colSede = BuscarColumna(normalizados, ["sede codigo", "sede id", "sede"]);
        var colTurno = BuscarColumna(normalizados, ["turno"]);
        var colFacultad = BuscarColumna(normalizados, ["facultad"]);

        IEnumerable<List<string>> filtradas = guardada.Filas;
        filtradas = FiltrarSede(filtradas, colSede, sedes);
        filtradas = FiltrarIgual(filtradas, colTurno, turnos);
        filtradas = FiltrarIgual(filtradas, colFacultad, facultades);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var buscado = Normalizar(q);
            filtradas = filtradas.Where(f => f.Any(v => Normalizar(v).Contains(buscado)));
        }
        var lista = filtradas.ToList();

        tamanio = Math.Clamp(tamanio, 1, MaxFilasPorPagina);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(lista.Count / (double)tamanio));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        return new PlanificacionInicialPaginaDto
        {
            Cargada = true,
            Archivo = guardada.NombreArchivo,
            SubidoEn = guardada.SubidoEn,
            SubidoPor = guardada.SubidoPor,
            Anio = guardada.Anio,
            Cuatrimestre = guardada.Cuatrimestre,
            Encabezados = guardada.Encabezados,
            Faltantes = ColumnasFaltantes(guardada.Encabezados),
            TotalFilas = guardada.Filas.Count,
            TotalFiltradas = lista.Count,
            Pagina = pagina,
            TamanioPagina = tamanio,
            TotalPaginas = totalPaginas,
            Filas = lista.Skip((pagina - 1) * tamanio).Take(tamanio).ToList(),
            Opciones = new OpcionesFiltroDto
            {
                Sedes = ValoresDistintos(guardada.Filas, colSede),
                Facultades = ValoresDistintos(guardada.Filas, colFacultad),
            },
        };
    }

    /// <summary>Deja las filas cuya celda coincide con alguno de los valores (sin importar mayúsculas ni acentos).</summary>
    private static IEnumerable<List<string>> FiltrarIgual(IEnumerable<List<string>> filas, int? col, string[]? valores)
    {
        // Si el Excel no tiene esa columna, o no se eligió ningún valor, el filtro no aplica (no deja la tabla vacía)
        var buscados = Buscados(valores);
        if (col == null || buscados.Count == 0) return filas;
        return filas.Where(f => buscados.Contains(Normalizar(f[col.Value])));
    }

    private static HashSet<string> Buscados(string[]? valores) =>
        (valores ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(Normalizar).ToHashSet();

    /// <summary>
    /// La sede viene de la tabla `sedes` ("Online") y en el Excel puede traer un prefijo ("UADE ONLINE"): se acepta la
    /// coincidencia exacta o como palabra completa, sin importar mayúsculas ni acentos.
    /// </summary>
    private static IEnumerable<List<string>> FiltrarSede(IEnumerable<List<string>> filas, int? col, string[]? valores)
    {
        var buscados = Buscados(valores);
        if (col == null || buscados.Count == 0) return filas;
        return filas.Where(f =>
        {
            var celda = Normalizar(f[col.Value]);
            return buscados.Contains(celda) || celda.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(buscados.Contains);
        });
    }

    private static List<string> ValoresDistintos(List<List<string>> filas, int? col)
    {
        if (col == null) return [];
        return filas.Select(f => f[col.Value]).Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.Create(new CultureInfo("es-AR"), true))
            .ToList();
    }

    internal static int? BuscarColumna(List<string> normalizados, string[] candidatos)
    {
        foreach (var c in candidatos)
        {
            var idx = normalizados.IndexOf(c);
            if (idx >= 0) return idx;
        }
        for (var i = 0; i < normalizados.Count; i++)
            foreach (var c in candidatos)
                if (normalizados[i].Contains(c)) return i;
        return null;
    }

    // internal: también lo usa DemandaExcelService para filtrar y agrupar sin importar acentos ni mayúsculas
    internal static string Normalizar(string texto)
    {
        var sinAcentos = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        var limpio = Regex.Replace(sinAcentos.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
        return Regex.Replace(limpio, @"\s+", " ");
    }
}
