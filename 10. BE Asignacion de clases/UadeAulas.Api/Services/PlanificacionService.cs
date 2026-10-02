using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;

namespace UadeAulas.Api.Services;

public class PlanificacionService
{
    private static readonly string[] DiasValidos =
        { "Lunes", "Martes", "Miercoles", "Jueves", "Viernes", "Sabado", "Domingo" };

    private readonly Repository _repository;

    public PlanificacionService(Repository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Valida y carga un Excel de planificación, reemplazando todo lo existente para el período indicado.
    /// No modifica la base si hay errores de validación.
    /// </summary>
    public async Task<CargaPlanificacionResultDto> CargarPlanificacionAsync(Stream excelStream, string cuatrimestreRaw, string anioRaw)
    {
        var resultado = new CargaPlanificacionResultDto();

        if (!TryParseAnio(anioRaw, out var anio))
        {
            resultado.Error = $"El año '{anioRaw}' no es válido.";
            return resultado;
        }

        if (!TryParseCuatrimestre(cuatrimestreRaw, out var cuatrimestre))
        {
            resultado.Error = $"El cuatrimestre '{cuatrimestreRaw}' no es válido (debe ser 1 o 2).";
            return resultado;
        }

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(excelStream);
        }
        catch (Exception ex)
        {
            resultado.Error = $"No se pudo leer el archivo Excel: {ex.Message}";
            return resultado;
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                resultado.Error = "El archivo Excel no tiene hojas.";
                return resultado;
            }

            var headerRow = worksheet.FirstRowUsed();
            if (headerRow == null)
            {
                resultado.Error = "El archivo Excel está vacío.";
                return resultado;
            }

            var headerMap = new Dictionary<string, int>();
            foreach (var cell in headerRow.CellsUsed())
            {
                var key = Normalizar(cell.GetString());
                if (!string.IsNullOrEmpty(key) && !headerMap.ContainsKey(key))
                    headerMap[key] = cell.Address.ColumnNumber;
            }

            int? Col(params string[] candidatos)
            {
                foreach (var c in candidatos)
                    if (headerMap.TryGetValue(c, out var idx)) return idx;
                foreach (var (headerKey, idx) in headerMap)
                    foreach (var c in candidatos)
                        if (headerKey.Contains(c)) return idx;
                return null;
            }

            var colMateria = Col("materia codigo", "codigo materia", "materia_codigo", "materia");
            var colSede = Col("sede codigo", "sede_id", "sede");
            var colTurno = Col("turno");
            var colDias = Col("dias", "dia");
            var colHorarioDesde = Col("horario desde", "hora desde", "desde");
            var colHorarioHasta = Col("horario hasta", "hora hasta", "hasta");
            var colHorarioCombinado = Col("horario desde hasta", "horario");
            var colAula = Col("aula codigo", "aula_id", "aula");
            var colInscriptos = Col("inscriptos");
            var colDocentes = Col("docente s", "docentes", "docente");
            var colRegimen = Col("regimen");
            var colIdioma = Col("idioma");
            var colRegimenIdioma = Col("regimen idioma");

            var faltantes = new List<string>();
            if (colMateria == null) faltantes.Add("Materia (código)");
            if (colSede == null) faltantes.Add("Sede");
            if (colTurno == null) faltantes.Add("Turno");
            if (colDias == null) faltantes.Add("Día");
            var tieneHorarioSeparado = colHorarioDesde != null && colHorarioHasta != null;
            if (!tieneHorarioSeparado && colHorarioCombinado == null) faltantes.Add("Horario desde/hasta");

            if (faltantes.Count > 0)
            {
                resultado.Error = $"Al archivo le faltan columnas obligatorias: {string.Join(", ", faltantes)}.";
                return resultado;
            }

            var sedes = await _repository.GetSedesAsync();
            var materias = await _repository.GetMateriasAsync();
            var aulas = await _repository.GetAulasAsync();
            var docentesExistentes = await _repository.GetDocentesAsync();
            var docentesPorNombre = docentesExistentes
                .GroupBy(d => d.Nombre.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First().Id);
            var docentesNuevos = new HashSet<string>();

            var errores = new List<string>();
            var advertencias = new List<string>();
            var filasParseadas = new List<(int FilaExcel, PlanificacionFilaInsert Fila, List<string> Docentes)>();

            foreach (var row in worksheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
            {
                var filaExcel = row.RowNumber();

                var materiaCodigo = row.Cell(colMateria!.Value).GetString().Trim();
                var sedeTexto = row.Cell(colSede!.Value).GetString().Trim();
                var turnoTexto = row.Cell(colTurno!.Value).GetString().Trim();
                var diasTexto = row.Cell(colDias!.Value).GetString().Trim();

                if (materiaCodigo.Length == 0 && sedeTexto.Length == 0 && turnoTexto.Length == 0 && diasTexto.Length == 0)
                    continue; // fila en blanco

                var erroresFila = new List<string>();

                var materia = materias.FirstOrDefault(m => string.Equals(m.Codigo, materiaCodigo, StringComparison.OrdinalIgnoreCase));
                if (materia == null)
                    erroresFila.Add($"la materia '{materiaCodigo}' no existe en el catálogo");

                var sede = sedes.FirstOrDefault(s =>
                    string.Equals(s.Codigo, sedeTexto, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s.Nombre, sedeTexto, StringComparison.OrdinalIgnoreCase));
                if (sede == null)
                    erroresFila.Add($"la sede '{sedeTexto}' no existe en el catálogo");

                var turno = NormalizarTurno(turnoTexto);
                if (turno == null)
                    erroresFila.Add($"el turno '{turnoTexto}' no es válido (Mañana/Tarde/Noche)");

                var dias = ParsearDias(diasTexto, out var diasInvalidos);
                if (dias.Length == 0 || diasInvalidos.Count > 0)
                    erroresFila.Add(diasInvalidos.Count > 0
                        ? $"el/los día(s) '{string.Join(", ", diasInvalidos)}' no son válidos"
                        : $"no se especificó ningún día válido");

                TimeOnly horarioDesde = default, horarioHasta = default;
                var horarioOk = false;
                if (tieneHorarioSeparado)
                {
                    horarioOk = TryParseHora(row.Cell(colHorarioDesde!.Value), out horarioDesde) &&
                                TryParseHora(row.Cell(colHorarioHasta!.Value), out horarioHasta);
                }
                else
                {
                    var texto = row.Cell(colHorarioCombinado!.Value).GetString().Trim();
                    horarioOk = TryParseRangoHorario(texto, out horarioDesde, out horarioHasta);
                }
                if (!horarioOk)
                    erroresFila.Add("el horario desde/hasta no tiene un formato válido");
                else if (horarioHasta <= horarioDesde)
                    erroresFila.Add("el horario 'hasta' debe ser posterior al horario 'desde'");

                var inscriptos = 0;
                if (colInscriptos.HasValue)
                {
                    var texto = row.Cell(colInscriptos.Value).GetString().Trim();
                    if (texto.Length > 0 && (!int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out inscriptos) || inscriptos < 0))
                        erroresFila.Add($"'Inscriptos' tiene un valor inválido ('{texto}')");
                }

                if (erroresFila.Count > 0)
                {
                    errores.Add($"Fila {filaExcel}: {string.Join("; ", erroresFila)}.");
                    continue;
                }

                // Aula: opcional, si no matchea solo advierte (no bloquea la carga)
                int? aulaId = null;
                int? aulaCapacidad = null;
                if (colAula.HasValue)
                {
                    var aulaCodigo = row.Cell(colAula.Value).GetString().Trim();
                    if (aulaCodigo.Length > 0)
                    {
                        var aula = aulas.FirstOrDefault(a =>
                            a.SedeId == sede!.Id && string.Equals(a.Codigo, aulaCodigo, StringComparison.OrdinalIgnoreCase));
                        if (aula == null)
                            advertencias.Add($"Fila {filaExcel}: el aula '{aulaCodigo}' no existe en la sede '{sede!.Codigo}', la clase quedará sin aula asignada.");
                        else
                        {
                            aulaId = aula.Id;
                            aulaCapacidad = aula.CapacidadTotal;
                        }
                    }
                }

                string? regimen = colRegimen.HasValue ? ValorOpcional(row.Cell(colRegimen.Value).GetString()) : null;
                string? idioma = colIdioma.HasValue ? ValorOpcional(row.Cell(colIdioma.Value).GetString()) : null;
                if (regimen == null && idioma == null && colRegimenIdioma.HasValue)
                {
                    var partes = row.Cell(colRegimenIdioma.Value).GetString().Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    regimen = partes.Length > 0 ? ValorOpcional(partes[0]) : null;
                    idioma = partes.Length > 1 ? ValorOpcional(partes[1]) : null;
                }

                var docentesFila = new List<string>();
                if (colDocentes.HasValue)
                {
                    var texto = row.Cell(colDocentes.Value).GetString();
                    docentesFila = ParsearDocentes(texto);
                    foreach (var nombre in docentesFila)
                        if (!docentesPorNombre.ContainsKey(nombre.ToLowerInvariant()))
                            docentesNuevos.Add(nombre);
                }

                var fila = new PlanificacionFilaInsert
                {
                    SedeId = sede!.Id,
                    MateriaCodigo = materia!.Codigo,
                    Turno = turno!,
                    Dias = dias,
                    HorarioDesde = horarioDesde,
                    HorarioHasta = horarioHasta,
                    Regimen = regimen,
                    Idioma = idioma,
                    Inscriptos = inscriptos,
                    AulaId = aulaId,
                    AulaCapacidadTotal = aulaCapacidad
                };

                filasParseadas.Add((filaExcel, fila, docentesFila));
            }

            if (errores.Count > 0)
            {
                resultado.Errores = errores;
                resultado.Advertencias = advertencias;
                resultado.Error = $"Se encontraron {errores.Count} error(es) en el archivo. No se modificó la planificación existente.";
                return resultado;
            }

            if (filasParseadas.Count == 0)
            {
                resultado.Error = "El archivo no contiene filas de datos para cargar.";
                return resultado;
            }

            // Crear docentes nuevos antes de resolver los ids de cada fila
            foreach (var nombre in docentesNuevos)
            {
                var id = await _repository.CreateDocenteAsync(nombre);
                docentesPorNombre[nombre.ToLowerInvariant()] = id;
            }

            foreach (var (_, fila, docentesFila) in filasParseadas)
                fila.DocenteIds = docentesFila.Select(n => docentesPorNombre[n.ToLowerInvariant()]).Distinct().ToList();

            var ciclo = $"{anio}-{cuatrimestre}";
            var periodo = await _repository.GetPeriodoByCicloAsync(ciclo);
            int periodoId;
            if (periodo != null)
            {
                periodoId = periodo.Id;
            }
            else
            {
                var nombre = $"{(cuatrimestre == 1 ? "Primer" : "Segundo")} Cuatrimestre {anio}";
                var (fechaDesde, fechaHasta) = cuatrimestre == 1
                    ? (new DateTime(anio, 3, 1), new DateTime(anio, 7, 15))
                    : (new DateTime(anio, 8, 1), new DateTime(anio, 12, 15));
                periodoId = await _repository.CreatePeriodoAsync(ciclo, nombre, fechaDesde, fechaHasta);
                periodo = new Models.Periodo { Id = periodoId, Ciclo = ciclo, Nombre = nombre };
            }

            var filasInsert = filasParseadas.Select(f => f.Fila).ToList();
            await _repository.ReplacePlanificacionPeriodoAsync(periodoId, filasInsert);

            resultado.Exito = true;
            resultado.Periodo = periodo.Nombre;
            resultado.FilasLeidas = filasInsert.Count;
            resultado.ClasesCreadas = filasInsert.Count;
            resultado.AsignacionesCreadas = filasInsert.Count(f => f.AulaId.HasValue);
            resultado.DocentesCreados = docentesNuevos.Count;
            resultado.Advertencias = advertencias;
            return resultado;
        }
    }

    private static bool TryParseAnio(string raw, out int anio)
    {
        anio = 0;
        var match = Regex.Match(raw ?? string.Empty, @"\d{4}");
        return match.Success && int.TryParse(match.Value, out anio) && anio is >= 2000 and <= 2100;
    }

    private static bool TryParseCuatrimestre(string raw, out int cuatrimestre)
    {
        cuatrimestre = 0;
        var texto = Normalizar(raw ?? string.Empty);
        if (texto.Contains('1') || texto.Contains("primer")) { cuatrimestre = 1; return true; }
        if (texto.Contains('2') || texto.Contains("segundo")) { cuatrimestre = 2; return true; }
        return false;
    }

    private static string Normalizar(string texto)
    {
        var sinAcentos = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        var limpio = Regex.Replace(sinAcentos.ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
        return Regex.Replace(limpio, @"\s+", " ");
    }

    private static string? NormalizarTurno(string texto)
    {
        return Normalizar(texto) switch
        {
            "manana" => "MANANA",
            "tarde" => "TARDE",
            "noche" => "NOCHE",
            _ => null
        };
    }

    private static string[] ParsearDias(string texto, out List<string> invalidos)
    {
        invalidos = new List<string>();
        var partes = Regex.Split(texto, @"\s+y\s+|[,;/]")
            .Select(p => p.Trim())
            .Where(p => p.Length > 0)
            .ToList();

        var dias = new List<string>();
        foreach (var parte in partes)
        {
            var normalizado = Normalizar(parte);
            var canonico = DiasValidos.FirstOrDefault(d => Normalizar(d) == normalizado);
            if (canonico == null)
                invalidos.Add(parte);
            else if (!dias.Contains(canonico))
                dias.Add(canonico);
        }
        return dias.ToArray();
    }

    private static bool TryParseHora(IXLCell cell, out TimeOnly hora)
    {
        hora = default;
        if (cell.IsEmpty()) return false;
        if (cell.TryGetValue(out TimeSpan ts)) { hora = TimeOnly.FromTimeSpan(ts); return true; }
        if (cell.TryGetValue(out DateTime dt)) { hora = TimeOnly.FromDateTime(dt); return true; }

        var texto = cell.GetString().Trim().Replace('.', ':').Replace(',', ':');
        return TimeOnly.TryParse(texto, CultureInfo.InvariantCulture, out hora);
    }

    private static bool TryParseRangoHorario(string texto, out TimeOnly desde, out TimeOnly hasta)
    {
        desde = default;
        hasta = default;
        var partes = Regex.Split(texto, @"\s*-\s*|\s+a\s+", RegexOptions.IgnoreCase);
        if (partes.Length != 2) return false;

        var d = partes[0].Trim().Replace('.', ':').Replace(',', ':');
        var h = partes[1].Trim().Replace('.', ':').Replace(',', ':');
        return TimeOnly.TryParse(d, CultureInfo.InvariantCulture, out desde) &&
               TimeOnly.TryParse(h, CultureInfo.InvariantCulture, out hasta);
    }

    private static string? ValorOpcional(string texto)
    {
        var limpio = texto.Trim();
        return limpio.Length == 0 ? null : limpio;
    }

    private static List<string> ParsearDocentes(string texto)
    {
        return Regex.Split(texto ?? string.Empty, @"\s*[,;/]\s*|\s+y\s+")
            .Select(n => n.Trim())
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
