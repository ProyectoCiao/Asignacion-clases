using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;

namespace UadeAulas.Api.Services;

public class ReglaNegocioException(string mensaje, int estado = 400, List<ConflictoDto>? conflictos = null) : Exception(mensaje)
{
    public int Estado { get; } = estado;
    public List<ConflictoDto>? Conflictos { get; } = conflictos;
}

/// <summary>
/// Lee las filas de la planificación guardada como clases (clave = columna "Id"), resuelve su aula (edición en
/// asignacion_aula o, si no hay, la del Excel cruzada con `aulas` por nombre) y le suma la demanda del Excel de demanda.
/// También registra las inscripciones de alumnos en ese Excel de demanda.
/// </summary>
public class ClasesExcelService
{
    private record ClaseBase(
        string Id, string Numero, string Periodo, string Ciclo, string Facultad, string Sede, string Turno, string Horario,
        string Dias, string CodigoMateria, string Materia, string Docentes, string AulaExcel);

    private record CacheClases(int PlanificacionId, List<ClaseBase> Clases);

    private static readonly object CacheLock = new();
    private static CacheClases? _cache;

    private readonly PlanificacionExcelRepository _planificacion;
    private readonly AsignacionAulaRepository _asignaciones;
    private readonly TablasMaestrasRepository _maestras;
    private readonly DemandaExcelService _demanda;
    private readonly AsignacionAlumnoRepository _alumnos;

    public ClasesExcelService(
        PlanificacionExcelRepository planificacion, AsignacionAulaRepository asignaciones,
        TablasMaestrasRepository maestras, DemandaExcelService demanda, AsignacionAlumnoRepository alumnos)
    {
        _planificacion = planificacion;
        _asignaciones = asignaciones;
        _maestras = maestras;
        _demanda = demanda;
        _alumnos = alumnos;
    }

    public async Task<ResumenAsignacionAlumnosDto> GetResumenAsignacionAlumnosAsync() => ADto(await _alumnos.GetResumenAsync());

    private static ResumenAsignacionAlumnosDto ADto(ResumenAsignacionAlumnos r) => new()
    {
        Filas = r.Filas, Alumnos = r.Alumnos, Opcion1 = r.Opcion1, Opcion2 = r.Opcion2, GeneradoEn = r.GeneradoEn, GeneradoPor = r.GeneradoPor,
    };

    /// <summary>
    /// "Asignar alumnos a aulas": distribuye a los alumnos del Excel de demanda entre las clases —y por lo tanto entre sus
    /// aulas— según lo que eligieron: la clase de "Id Clase" queda como primera opción (silla ocupada) y la de
    /// "Id Clase 2da Opcion" como segunda (tentativa). Reemplaza la asignación anterior. No mueve clases de aula ni cambia
    /// el Excel de demanda; el cupo no recorta nada: un aula con más alumnos que sillas queda a la vista para reordenarla.
    /// </summary>
    public async Task<ResumenAsignacionAlumnosDto> GenerarAsignacionAlumnosAsync(string usuario)
    {
        var bases = await ObtenerBasesAsync() ?? throw new ReglaNegocioException("Todavía no se subió la planificación.", 404);
        var demanda = await _demanda.ObtenerDatosAsync()
            ?? throw new ReglaNegocioException("Todavía no se subió el Excel de demanda.", 404);

        var n = demanda.Encabezados.Select(ExcelCrudoService.Normalizar).ToList();
        var colLegajo = n.FindIndex(h => h.Contains("legajo"));
        var colAlumno = n.FindIndex(h => h is "alumno" or "nombre");
        var colId1 = n.FindIndex(h => h == "id clase");
        var colId2 = n.FindIndex(h => h.Contains("id clase") && h.Contains("2da"));
        if (colLegajo < 0) throw new ReglaNegocioException("El Excel de demanda no tiene la columna \"Legajo\".");
        if (colId1 < 0) throw new ReglaNegocioException("El Excel de demanda no tiene la columna \"Id Clase\".");

        var existentes = bases.Select(b => b.Id).ToHashSet();
        var filas = new Dictionary<(string, string, short), AsignacionAlumnoFila>();
        var inexistentes = 0;
        string Celda(List<string> f, int c) => c >= 0 && c < f.Count ? f[c].Trim() : string.Empty;

        foreach (var fila in demanda.Filas)
        {
            var legajo = Celda(fila, colLegajo);
            if (legajo.Length == 0) continue;
            var alumno = Celda(fila, colAlumno);
            var id1 = Celda(fila, colId1);
            var id2 = Celda(fila, colId2);

            if (id1.Length > 0)
            {
                if (existentes.Contains(id1)) filas[(legajo, id1, 1)] = new AsignacionAlumnoFila(legajo, alumno, id1, 1);
                else inexistentes++;
            }
            if (id2.Length > 0 && id2 != id1)
            {
                if (existentes.Contains(id2)) filas[(legajo, id2, 2)] = new AsignacionAlumnoFila(legajo, alumno, id2, 2);
                else inexistentes++;
            }
        }

        await _alumnos.ReemplazarAsync(filas.Values, usuario);
        var resumen = ADto(await _alumnos.GetResumenAsync());
        resumen.IdsInexistentes = inexistentes;
        return resumen;
    }

    public async Task<ClasesPaginaDto> GetPaginaAsync(
        int pagina, int tamanio, string[]? sedes, string[]? turnos, int[]? edificios, string[]? pisos, int[]? aulas,
        string? q, bool? soloConAula, string[]? periodos = null, string[]? materias = null)
    {
        var bases = await ObtenerBasesAsync();
        if (bases == null) return new ClasesPaginaDto { Cargada = false };

        var todas = await ArmarAsync(bases);
        var buscadasSedes = Normalizados(sedes);
        var buscadosTurnos = Normalizados(turnos);
        var buscadosPisos = Normalizados(pisos);
        var buscada = string.IsNullOrWhiteSpace(q) ? null : ExcelCrudoService.Normalizar(q);

        IEnumerable<ClaseDto> filtradas = todas.Clases;
        if (buscadasSedes.Count > 0)
            filtradas = filtradas.Where(c => CoincideSede(c.Sede, buscadasSedes));
        if (buscadosTurnos.Count > 0)
            filtradas = filtradas.Where(c => buscadosTurnos.Contains(ExcelCrudoService.Normalizar(c.Turno)));
        // Materia: nombre de `materia.materia_nombre`, comparado con la columna Materia del Excel sin acentos ni mayúsculas
        // y, sobre todo, por código de materia (el nombre del Excel a veces viene abreviado)
        var buscadasMaterias = Normalizados(materias);
        if (buscadasMaterias.Count > 0)
        {
            var codigos = await _maestras.GetCodigosDeMateriasAsync(materias!.Where(m => !string.IsNullOrWhiteSpace(m)).ToArray());
            filtradas = filtradas.Where(c =>
                codigos.Contains(c.CodigoMateria.Trim().ToUpperInvariant())
                || buscadasMaterias.Contains(ExcelCrudoService.Normalizar(c.Materia)));
        }
        var buscadosPeriodos = Normalizados(periodos);
        if (buscadosPeriodos.Count > 0)
            filtradas = filtradas.Where(c => buscadosPeriodos.Contains(ExcelCrudoService.Normalizar(c.Periodo)));
        if (edificios is { Length: > 0 })
            filtradas = filtradas.Where(c => c.EdificioId != null && edificios.Contains(c.EdificioId.Value));
        if (buscadosPisos.Count > 0)
            filtradas = filtradas.Where(c => c.Piso != null && buscadosPisos.Contains(ExcelCrudoService.Normalizar(c.Piso)));
        if (aulas is { Length: > 0 })
            filtradas = filtradas.Where(c => c.AulaId != null && aulas.Contains(c.AulaId.Value));
        if (soloConAula == true)
            filtradas = filtradas.Where(c => c.AulaId != null);
        if (buscada != null)
            filtradas = filtradas.Where(c => ExcelCrudoService.Normalizar(
                $"{c.Id} {c.Numero} {c.Materia} {c.CodigoMateria} {c.Docentes} {c.Aula}").Contains(buscada));

        var lista = filtradas.ToList();
        tamanio = Math.Clamp(tamanio, 1, MaxPorPagina);
        var totalPaginas = Math.Max(1, (int)Math.Ceiling(lista.Count / (double)tamanio));
        pagina = Math.Clamp(pagina, 1, totalPaginas);

        return new ClasesPaginaDto
        {
            Cargada = true,
            HayDemanda = todas.HayDemanda,
            HayAsignaciones = todas.HayAsignaciones,
            TotalFiltradas = lista.Count,
            Pagina = pagina,
            TamanioPagina = tamanio,
            TotalPaginas = totalPaginas,
            Clases = lista.Skip((pagina - 1) * tamanio).Take(tamanio).ToList(),
        };
    }

    public const int MaxPorPagina = 50000;

    public async Task<ClaseDto?> GetClaseAsync(string id)
    {
        var bases = await ObtenerBasesAsync();
        if (bases == null) return null;
        var una = bases.Where(b => b.Id == id).ToList();
        if (una.Count == 0) return null;
        return (await ArmarAsync(una)).Clases[0];
    }

    /// <summary>Cambia el aula de una clase (null = sin aula). El Excel original no se modifica.</summary>
    public async Task<CambioAulaResultadoDto> CambiarAulaAsync(
        string claseId, int? aulaId, string usuario, string usuarioReal, bool forzarCupo = false)
    {
        var bases = await ObtenerBasesAsync() ?? throw new ReglaNegocioException("Todavía no se subió la planificación.", 404);
        if (bases.All(b => b.Id != claseId)) throw new ReglaNegocioException($"No existe la clase con Id {claseId}.", 404);

        var advertencias = new List<ConflictoDto>();
        if (aulaId != null)
        {
            var aulas = await _maestras.GetAulasAsync(null, null, null, null);
            var destino = aulas.FirstOrDefault(a => a.Id == aulaId) ?? throw new ReglaNegocioException($"No existe el aula {aulaId}.", 404);
            advertencias = await ValidarCambioAsync(bases, claseId, destino, forzarCupo);
        }

        await _asignaciones.GuardarAsync(claseId, aulaId, usuario, usuarioReal);
        return new CambioAulaResultadoDto { Clase = (await GetClaseAsync(claseId))!, Advertencias = advertencias };
    }

    /// <summary>
    /// Valida mover la clase al aula destino. Bloquean (409, con la lista de conflictos): choque de aula (otra clase del mismo
    /// período en esa aula, con día y horario en común) y cupo (inscriptos > sillas, salvo `forzarCupo`). El choque de
    /// docente (mismo docente, mismo período, día y horario en común) no depende del aula, así que no impide el cambio:
    /// se devuelve como advertencia.
    /// </summary>
    private async Task<List<ConflictoDto>> ValidarCambioAsync(List<ClaseBase> bases, string claseId, AulaMaestroDto destino, bool forzarCupo)
    {
        var armadas = (await ArmarAsync(bases)).Clases;
        var clase = armadas.First(c => c.Id == claseId);
        var bloqueos = new List<ConflictoDto>();
        var advertencias = new List<ConflictoDto>();

        if (clase.AulaId == destino.Id) return advertencias; // sigue en la misma aula: nada que validar

        if (clase.Demanda != null && destino.Capacidad != null && clase.Demanda > destino.Capacidad)
        {
            var cupo = new ConflictoDto
            {
                Tipo = "CUPO",
                Detalle = $"{destino.Nombre} tiene {destino.Capacidad} sillas y la clase {clase.Numero} tiene {clase.Demanda} inscriptos: faltan {clase.Demanda - destino.Capacidad}.",
            };
            (forzarCupo ? advertencias : bloqueos).Add(cupo);
        }

        foreach (var otra in armadas)
        {
            if (otra.Id == claseId || !SePisan(clase, otra)) continue;
            if (otra.AulaId == destino.Id)
                bloqueos.Add(new ConflictoDto
                {
                    Tipo = "CHOQUE_AULA",
                    ClaseId = otra.Id,
                    Detalle = $"{destino.Nombre} ya está ocupada por la clase {otra.Numero} ({otra.Materia}) los {otra.Dias}, {otra.Horario}.",
                });

            var propios = Docentes(clase);
            var comunes = Docentes(otra).Keys.Where(propios.ContainsKey).Select(k => propios[k]).ToList();
            if (comunes.Count > 0)
                advertencias.Add(new ConflictoDto
                {
                    Tipo = "CHOQUE_DOCENTE",
                    ClaseId = otra.Id,
                    Detalle = $"{string.Join(", ", comunes)} también dicta la clase {otra.Numero} ({otra.Materia}) los {otra.Dias}, {otra.Horario}.",
                });
        }

        if (bloqueos.Count > 0)
            throw new ReglaNegocioException(
                "No se puede mover la clase: " + string.Join(" ", bloqueos.Select(b => b.Detalle)), 409, bloqueos);
        return advertencias;
    }

    /// <summary>Dos clases se pisan si cursan en períodos que coinciden y tienen algún día y horarios superpuestos.</summary>
    private static bool SePisan(ClaseDto a, ClaseDto b)
    {
        if (!PeriodosCoinciden(a, b)) return false;
        var diasA = DiasDe(a.Dias);
        var diasB = DiasDe(b.Dias);
        if (!diasA.Overlaps(diasB)) return false;
        var ha = Horario(a.Horario);
        var hb = Horario(b.Horario);
        return ha != null && hb != null && ha.Value.Desde < hb.Value.Hasta && hb.Value.Desde < ha.Value.Hasta;
    }

    /// <summary>Mismo ciclo y mismo período; un período anual se superpone con cualquier cuatrimestre del ciclo.</summary>
    private static bool PeriodosCoinciden(ClaseDto a, ClaseDto b)
    {
        if (ExcelCrudoService.Normalizar(a.Ciclo) != ExcelCrudoService.Normalizar(b.Ciclo)) return false;
        var pa = ExcelCrudoService.Normalizar(a.Periodo);
        var pb = ExcelCrudoService.Normalizar(b.Periodo);
        if (pa == pb) return true;
        var anual = (string p) => p.StartsWith("anual");
        var verano = (string p) => p.StartsWith("verano");
        return (anual(pa) || anual(pb)) && !verano(pa) && !verano(pb);
    }

    private static HashSet<string> DiasDe(string dias) =>
        dias.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(ExcelCrudoService.Normalizar).ToHashSet();

    private static (int Desde, int Hasta)? Horario(string horario)
    {
        var partes = horario.Split('-', StringSplitOptions.TrimEntries);
        if (partes.Length != 2) return null;
        static int? Minutos(string hora)
        {
            var p = hora.Split(':');
            return p.Length == 2 && int.TryParse(p[0], out var h) && int.TryParse(p[1], out var m) ? h * 60 + m : null;
        }
        var d = Minutos(partes[0]);
        var h = Minutos(partes[1]);
        return d != null && h != null ? (d.Value, h.Value) : null;
    }

    /// <summary>Docentes de la clase: "Apellido, Nombre - Apellido, Nombre"; "-" o vacío = sin docente.</summary>
    private static Dictionary<string, string> Docentes(ClaseDto clase)
    {
        var resultado = new Dictionary<string, string>();
        foreach (var d in clase.Docentes.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            if (d != "-") resultado.TryAdd(ExcelCrudoService.Normalizar(d), d); // clave sin acentos, valor tal cual el Excel
        return resultado;
    }

    /// <summary>Quita la edición: la clase vuelve al aula del Excel.</summary>
    public async Task<ClaseDto> RestaurarAulaAsync(string claseId)
    {
        var bases = await ObtenerBasesAsync() ?? throw new ReglaNegocioException("Todavía no se subió la planificación.", 404);
        if (bases.All(b => b.Id != claseId)) throw new ReglaNegocioException($"No existe la clase con Id {claseId}.", 404);
        await _asignaciones.QuitarAsync(claseId);
        return (await GetClaseAsync(claseId))!;
    }

    /// <summary>
    /// Agrega al Excel de demanda la inscripción de un alumno (primera opción y, si viene, segunda). Completa cada columna
    /// del Excel de demanda por su encabezado; las que no reconoce quedan vacías.
    /// </summary>
    public async Task<int> InscribirAsync(InscripcionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Legajo)) throw new ReglaNegocioException("Falta el legajo del alumno.");
        if (string.IsNullOrWhiteSpace(request.Alumno)) throw new ReglaNegocioException("Falta el nombre del alumno.");

        var bases = await ObtenerBasesAsync() ?? throw new ReglaNegocioException("Todavía no se subió la planificación.", 404);
        var c1 = bases.FirstOrDefault(b => b.Id == request.ClaseId?.Trim())
            ?? throw new ReglaNegocioException($"No existe la clase con Id {request.ClaseId}.", 404);
        ClaseBase? c2 = null;
        if (!string.IsNullOrWhiteSpace(request.ClaseId2))
        {
            c2 = bases.FirstOrDefault(b => b.Id == request.ClaseId2.Trim())
                ?? throw new ReglaNegocioException($"No existe la clase con Id {request.ClaseId2} (segunda opción).", 404);
            if (c2.Id == c1.Id) throw new ReglaNegocioException("La segunda opción no puede ser la misma clase.");
            if (c2.CodigoMateria != c1.CodigoMateria)
                throw new ReglaNegocioException("La segunda opción tiene que ser otra comisión de la misma materia.");
        }

        var demanda = await _demanda.ObtenerDatosAsync()
            ?? throw new ReglaNegocioException("Todavía no se subió el Excel de demanda.", 404);
        var normalizados = demanda.Encabezados.Select(ExcelCrudoService.Normalizar).ToList();
        var colId = normalizados.IndexOf("id clase");
        if (colId < 0) throw new ReglaNegocioException("El Excel de demanda no tiene la columna \"Id Clase\": no se puede registrar la inscripción.");
        var colLegajo = normalizados.FindIndex(h => h.Contains("legajo"));

        var legajo = request.Legajo.Trim();
        var colCodigo = normalizados.FindIndex(h => h.Contains("codigo materia") && !h.Contains("2da"));
        if (colLegajo >= 0)
        {
            var anteriores = demanda.Filas.Where(f => f[colLegajo].Trim() == legajo).ToList();
            if (anteriores.Any(f => f[colId].Trim() == c1.Id))
                throw new ReglaNegocioException($"El legajo {legajo} ya está inscripto en la clase {c1.Id}.", 409);
            if (colCodigo >= 0 && anteriores.Any(f => f[colCodigo].Trim() == c1.CodigoMateria))
                throw new ReglaNegocioException($"El legajo {legajo} ya está inscripto en la materia {c1.CodigoMateria}.", 409);
        }

        var fila = normalizados.Select(h => ValorColumna(h, request, c1, c2)).ToList();
        if (!await _demanda.AgregarFilaAsync(fila))
            throw new ReglaNegocioException("Todavía no se subió el Excel de demanda.", 404);
        return demanda.Filas.Count + 1;
    }

    private static string ValorColumna(string h, InscripcionRequest r, ClaseBase c1, ClaseBase? c2)
    {
        if (h.Contains("legajo")) return r.Legajo.Trim();
        if (h is "alumno" or "nombre") return r.Alumno.Trim();

        var segunda = h.Contains("2da");
        var c = segunda ? c2 : c1;
        if (c == null) return string.Empty;
        if (h.Contains("id clase")) return c.Id;
        if (h.Contains("codigo materia")) return c.CodigoMateria;
        if (h.Contains("materia")) return c.Materia;
        if (h.StartsWith("clase")) return c.Numero;
        if (h.Contains("ciclo")) return c.Ciclo;
        if (h.Contains("periodo")) return c.Periodo;
        if (h.Contains("sede")) return c.Sede;
        if (h.Contains("turno")) return c.Turno;
        if (h.Contains("dias")) return c.Dias;
        if (h.Contains("horario")) return c.Horario;
        if (h.Contains("docente")) return c.Docentes;
        return string.Empty;
    }

    // ---- armado ----

    private record ClasesArmadas(List<ClaseDto> Clases, bool HayDemanda, bool HayAsignaciones);

    private async Task<ClasesArmadas> ArmarAsync(List<ClaseBase> bases)
    {
        var aulas = await _maestras.GetAulasAsync(null, null, null, null);
        var porNombre = new Dictionary<string, AulaMaestroDto>();
        foreach (var a in aulas)
            porNombre.TryAdd(Clave(a.Nombre), a);
        var porId = aulas.ToDictionary(a => a.Id);
        var ediciones = await _asignaciones.GetTodasAsync();
        var demanda = await ContarDemandaPorClaseAsync();
        var asignados = await _alumnos.GetConteosAsync();
        var hayAsignaciones = asignados.Count > 0;

        var clases = new List<ClaseDto>(bases.Count);
        foreach (var b in bases)
        {
            AulaMaestroDto? aula = null;
            var modificada = false;
            AsignacionAula? edicion = null;
            if (ediciones.TryGetValue(b.Id, out edicion))
            {
                aula = edicion.AulaId != null && porId.TryGetValue(edicion.AulaId.Value, out var editada) ? editada : null;
            }
            else if (b.AulaExcel.Length > 0 && b.AulaExcel != "-")
            {
                porNombre.TryGetValue(Clave(b.AulaExcel), out aula);
            }

            AulaMaestroDto? original = null;
            if (b.AulaExcel.Length > 0 && b.AulaExcel != "-") porNombre.TryGetValue(Clave(b.AulaExcel), out original);
            if (edicion != null) modificada = aula?.Id != original?.Id;

            int? dem = demanda?.GetValueOrDefault(b.Id, 0);
            clases.Add(new ClaseDto
            {
                Id = b.Id, Numero = b.Numero, Periodo = b.Periodo, Ciclo = b.Ciclo, Facultad = b.Facultad, Sede = b.Sede,
                Turno = b.Turno, Horario = b.Horario, Dias = b.Dias, CodigoMateria = b.CodigoMateria, Materia = b.Materia,
                Docentes = b.Docentes, AulaExcel = b.AulaExcel,
                AulaId = aula?.Id, Aula = aula?.Nombre, Capacidad = aula?.Capacidad, SedeAulaId = aula?.SedeId,
                EdificioId = aula?.EdificioId, Edificio = aula?.Edificio, Piso = aula?.Piso, Tipo = aula?.Tipo,
                Modificada = modificada,
                ModificadaPor = modificada ? edicion?.ModificadoPor : null,
                ModificadaEn = modificada ? edicion?.ModificadoEn : null,
                Demanda = dem,
                Asignados1 = hayAsignaciones ? asignados.GetValueOrDefault(b.Id).Primera : null,
                Asignados2 = hayAsignaciones ? asignados.GetValueOrDefault(b.Id).Segunda : null,
                SillasVacias = dem != null && aula?.Capacidad != null ? aula.Capacidad - dem : null,
            });
        }
        return new ClasesArmadas(clases, demanda != null, hayAsignaciones);
    }

    /// <summary>Inscripciones en primera opción por Id de clase, según el Excel de demanda; null si no hay o no trae "Id Clase".</summary>
    private async Task<Dictionary<string, int>?> ContarDemandaPorClaseAsync()
    {
        var datos = await _demanda.ObtenerDatosAsync();
        if (datos == null) return null;
        var col = datos.Encabezados.Select(ExcelCrudoService.Normalizar).ToList().IndexOf("id clase");
        if (col < 0) return null;

        var conteo = new Dictionary<string, int>();
        foreach (var fila in datos.Filas)
        {
            var id = col < fila.Count ? fila[col].Trim() : string.Empty;
            if (id.Length == 0) continue;
            conteo[id] = conteo.GetValueOrDefault(id) + 1;
        }
        return conteo;
    }

    private async Task<List<ClaseBase>?> ObtenerBasesAsync()
    {
        var id = await _planificacion.GetIdVigenteAsync();
        if (id == null) return null;
        lock (CacheLock)
            if (_cache?.PlanificacionId == id) return _cache.Clases;

        var guardada = await _planificacion.ObtenerAsync();
        if (guardada == null) return null;

        var n = guardada.Encabezados.Select(ExcelCrudoService.Normalizar).ToList();
        int? Col(params string[] c) => ExcelCrudoService.BuscarColumna(n, c);
        var cId = Col("id") ?? throw new ReglaNegocioException("La planificación no tiene la columna \"Id\".");
        var cNum = Col("clase");
        var cPer = Col("periodo");
        var cCiclo = Col("ciclo");
        var cFac = Col("facultad");
        var cSede = Col("sede");
        var cTurno = Col("turno");
        var cHor = Col("horario");
        var cDias = Col("dias de cursada", "dias");
        var cCod = Col("codigomateria", "codigo materia");
        var cMat = Col("materia");
        var cDoc = Col("docentes", "docente");
        var cAula = Col("aula principal", "aula");

        string V(List<string> f, int? c) => c != null && c < f.Count ? f[c.Value].Trim() : string.Empty;
        var lista = guardada.Filas
            .Select(f => new ClaseBase(V(f, cId), V(f, cNum), V(f, cPer), V(f, cCiclo), V(f, cFac), V(f, cSede), V(f, cTurno),
                V(f, cHor), V(f, cDias), V(f, cCod), V(f, cMat), V(f, cDoc), V(f, cAula)))
            .Where(c => c.Id.Length > 0)
            .ToList();

        lock (CacheLock) _cache = new CacheClases(id.Value, lista);
        return lista;
    }

    private static string Clave(string nombre) => nombre.Trim().ToUpperInvariant();

    private static HashSet<string> Normalizados(string[]? valores) =>
        (valores ?? []).Where(v => !string.IsNullOrWhiteSpace(v)).Select(ExcelCrudoService.Normalizar).ToHashSet();

    /// <summary>La sede de `sedes` ("Online") puede venir con prefijo en el Excel ("UADE ONLINE"): exacta o palabra completa.</summary>
    private static bool CoincideSede(string sede, HashSet<string> buscadas)
    {
        var celda = ExcelCrudoService.Normalizar(sede);
        return buscadas.Contains(celda) || celda.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(buscadas.Contains);
    }
}
