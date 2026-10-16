using System.Globalization;
using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;
using UadeAulas.Api.Models;

namespace UadeAulas.Api.Services;

/// <summary>
/// Edición de la planificación por el backoffice: materia, docentes, turno, días, horario y aula de las clases.
/// Todo cambio llega como un lote que se valida sobre el estado FINAL del período y se aplica completo o no se
/// aplica. Reglas:
/// - Ninguna aula puede quedar con dos clases el mismo día en horarios superpuestos. Por eso mover una clase a
///   un aula ocupada obliga a mover también a la ocupante en el mismo lote (intercambio A↔B o rueda A→B→C→A).
/// - Ninguna clase puede quedar sin aula: el lote solo mueve clases de aula, nunca las desasigna.
/// - Un docente no puede dar dos clases el mismo día en horarios superpuestos.
/// Solo se informan choques en los que participa alguna clase del lote (los preexistentes no bloquean).
/// </summary>
public class EdicionPlanificacionService
{
    // Aulas virtuales: pueden tener muchas clases a la vez
    private static readonly HashSet<string> AulasSinChoque = new() { "REMOTA", "SIN_AULA" };

    private readonly Repository _repository;

    public EdicionPlanificacionService(Repository repository)
    {
        _repository = repository;
    }

    private class EstadoClase
    {
        public long Id { get; init; }
        public int NroClase { get; init; }
        public int SedeId { get; init; }
        public int InscriptosConReserva { get; init; }
        public string MateriaCodigo { get; set; } = string.Empty;
        public string Turno { get; set; } = string.Empty;
        public string[] Dias { get; set; } = Array.Empty<string>();
        public TimeOnly Desde { get; set; }
        public TimeOnly Hasta { get; set; }
        public int? AulaId { get; set; }
        public List<int> DocenteIds { get; set; } = new();

        public EstadoClase Copiar() => new()
        {
            Id = Id, NroClase = NroClase, SedeId = SedeId, InscriptosConReserva = InscriptosConReserva,
            MateriaCodigo = MateriaCodigo, Turno = Turno, Dias = Dias.ToArray(), Desde = Desde, Hasta = Hasta,
            AulaId = AulaId, DocenteIds = DocenteIds.ToList()
        };

        public string Horario => $"{Desde:HH\\:mm}-{Hasta:HH\\:mm}";
    }

    // ---------------------------------------------------------------- Lote de cambios

    public async Task<IResult> AplicarLoteAsync(int periodoId, LoteCambiosRequest request, string usuario)
    {
        var response = new LoteCambiosResponse();

        var periodo = await _repository.GetPeriodoByIdAsync(periodoId);
        if (periodo == null)
            return Results.NotFound(new { error = $"No existe el período {periodoId}." });

        var estado = periodo.Estado;
        if (estado == EstadoPlanificacion.Finalizada)
            return Results.Conflict(new { error = $"El {periodo.Nombre} está finalizado: para modificarlo hay que reabrirlo (con motivo)." });

        var motivo = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim();
        response.EsEmergencia = estado != EstadoPlanificacion.EnEdicion;
        if (response.EsEmergencia && motivo == null)
            return Rechazar(response, $"La planificación ya está congelada ({estado}): el cambio es de emergencia y requiere un motivo.");

        if (request.Cambios.Count == 0)
            return Rechazar(response, "El lote no tiene cambios.");

        var duplicadas = request.Cambios.GroupBy(c => c.ClaseId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicadas.Count > 0)
            return Rechazar(response, $"Cada clase puede aparecer una sola vez en el lote (repetidas: {string.Join(", ", duplicadas)}).");

        // Estado actual del período
        var (inicial, aulas, materias) = await CargarEstadoPeriodoAsync(periodoId);
        var docentes = (await _repository.GetDocentesAsync()).Select(d => d.Id).ToHashSet();
        var final = inicial.ToDictionary(kv => kv.Key, kv => kv.Value.Copiar());

        // 1) Validar cada cambio y aplicarlo al estado final en memoria
        foreach (var cambio in request.Cambios)
        {
            if (!final.TryGetValue(cambio.ClaseId, out var clase))
            {
                response.Errores.Add($"La clase {cambio.ClaseId} no existe en el {periodo.Nombre}.");
                continue;
            }
            response.Errores.AddRange(AplicarCambio(cambio, clase, aulas, materias, docentes)
                .Select(e => $"Clase {clase.NroClase}: {e}"));
        }
        if (response.Errores.Count > 0)
            return Rechazar(response, "Hay datos inválidos en el lote. No se aplicó ningún cambio.");

        var modificadas = request.Cambios.Select(c => final[c.ClaseId])
            .Where(c => Difiere(inicial[c.Id], c))
            .ToList();
        if (modificadas.Count == 0)
        {
            response.Exito = true;
            return Results.Ok(response);
        }

        // 2) Validar el estado final: choques de aula y de docente en los que participa una clase del lote
        var idsLote = request.Cambios.Select(c => c.ClaseId).ToHashSet();
        response.Errores.AddRange(ValidarChoquesAula(modificadas, final, inicial, idsLote, aulas, materias));
        response.Errores.AddRange(ValidarChoquesDocente(modificadas, final));
        if (response.Errores.Count > 0)
            return Rechazar(response, "El lote deja choques. No se aplicó ningún cambio.");

        // 3) Advertencias (no bloquean): más inscriptos que capacidad en el aula final
        foreach (var c in modificadas.Where(c => c.AulaId.HasValue))
        {
            var aula = aulas[c.AulaId!.Value];
            if (!AulasSinChoque.Contains(aula.TipoGrupo) && aula.CapacidadTotal < c.InscriptosConReserva)
                response.Advertencias.Add(
                    $"Clase {c.NroClase}: tiene {c.InscriptosConReserva} inscriptos y el aula {aula.Codigo} tiene capacidad {aula.CapacidadTotal}.");
        }

        // 4) Aplicar en una transacción con su auditoría
        var lote = Guid.NewGuid();
        var aplicar = modificadas.Select(c =>
        {
            var antes = inicial[c.Id];
            var aulaCambio = c.AulaId != antes.AulaId;
            return new ClaseEdicion
            {
                ClaseId = c.Id,
                MateriaCodigo = c.MateriaCodigo,
                Turno = c.Turno,
                Dias = c.Dias,
                HorarioDesde = c.Desde,
                HorarioHasta = c.Hasta,
                DocenteIds = MismosDocentes(antes, c) ? null : c.DocenteIds,
                AulaId = aulaCambio ? c.AulaId : null,
                SillasVacias = c.AulaId.HasValue ? aulas[c.AulaId.Value].CapacidadTotal - c.InscriptosConReserva : 0
            };
        }).ToList();

        var auditorias = modificadas.Select(c =>
        {
            var (antes, despues) = Diferencias(inicial[c.Id], c, aulas);
            return new Auditoria
            {
                Usuario = usuario,
                PeriodoId = periodoId,
                Entidad = "clase",
                EntidadId = c.Id,
                Accion = AccionAuditoria.Modificacion,
                EsEmergencia = response.EsEmergencia,
                Motivo = motivo,
                Antes = antes,
                Despues = despues,
                Lote = lote
            };
        }).ToList();

        await _repository.AplicarCambiosClasesAsync(periodoId, aplicar, auditorias);

        response.Exito = true;
        response.Lote = lote;
        response.ClasesModificadas = modificadas.Count;
        return Results.Ok(response);
    }

    private static IResult Rechazar(LoteCambiosResponse response, string error)
    {
        response.Exito = false;
        response.Error = error;
        return Results.BadRequest(response);
    }

    private static List<string> AplicarCambio(
        CambioEdicionDto cambio, EstadoClase clase, Dictionary<int, Aula> aulas,
        Dictionary<string, Materia> materias, HashSet<int> docentes)
    {
        var errores = new List<string>();

        if (cambio.MateriaCodigo != null)
        {
            var materia = materias.Values.FirstOrDefault(m =>
                string.Equals(m.Codigo, cambio.MateriaCodigo.Trim(), StringComparison.OrdinalIgnoreCase));
            if (materia == null) errores.Add($"la materia '{cambio.MateriaCodigo}' no existe en el catálogo");
            else clase.MateriaCodigo = materia.Codigo;
        }

        if (cambio.DocenteIds != null)
        {
            var inexistentes = cambio.DocenteIds.Where(id => !docentes.Contains(id)).ToList();
            if (inexistentes.Count > 0) errores.Add($"no existen los docentes {string.Join(", ", inexistentes)}");
            else clase.DocenteIds = cambio.DocenteIds.Distinct().ToList();
        }

        if (cambio.Turno != null)
        {
            var turno = PlanificacionService.NormalizarTurno(cambio.Turno);
            if (turno == null) errores.Add($"el turno '{cambio.Turno}' no es válido (Mañana/Tarde/Noche)");
            else clase.Turno = turno;
        }

        if (cambio.Dias != null)
        {
            var dias = PlanificacionService.ParsearDias(string.Join(",", cambio.Dias), out var invalidos);
            if (invalidos.Count > 0) errores.Add($"el/los día(s) '{string.Join(", ", invalidos)}' no son válidos");
            else if (dias.Length == 0) errores.Add("tiene que tener al menos un día");
            else clase.Dias = dias;
        }

        if (cambio.HorarioDesde != null)
        {
            if (TryParseHora(cambio.HorarioDesde, out var desde)) clase.Desde = desde;
            else errores.Add($"el horario desde '{cambio.HorarioDesde}' no es válido (HH:mm)");
        }
        if (cambio.HorarioHasta != null)
        {
            if (TryParseHora(cambio.HorarioHasta, out var hasta)) clase.Hasta = hasta;
            else errores.Add($"el horario hasta '{cambio.HorarioHasta}' no es válido (HH:mm)");
        }
        if (clase.Hasta <= clase.Desde)
            errores.Add($"el horario 'hasta' ({clase.Hasta:HH\\:mm}) debe ser posterior al 'desde' ({clase.Desde:HH\\:mm})");

        if (cambio.AulaId.HasValue)
        {
            if (!aulas.TryGetValue(cambio.AulaId.Value, out var aula))
                errores.Add($"el aula {cambio.AulaId} no existe");
            else if (aula.SedeId != clase.SedeId)
                errores.Add($"el aula {aula.Codigo} es de otra sede (lo edilicio no cambia de sede)");
            else if (!aula.Activa)
                errores.Add($"el aula {aula.Codigo} no está activa");
            else
                clase.AulaId = aula.Id;
        }

        return errores;
    }

    private static List<string> ValidarChoquesAula(
        List<EstadoClase> modificadas, Dictionary<long, EstadoClase> final, Dictionary<long, EstadoClase> inicial,
        HashSet<long> idsLote, Dictionary<int, Aula> aulas, Dictionary<string, Materia> materias)
    {
        var errores = new List<string>();
        var informados = new HashSet<(long, long)>();

        foreach (var c in modificadas)
        {
            if (!c.AulaId.HasValue || AulasSinChoque.Contains(aulas[c.AulaId.Value].TipoGrupo))
                continue;
            var aula = aulas[c.AulaId.Value];

            foreach (var o in final.Values.Where(o => o.Id != c.Id && o.AulaId == c.AulaId && Solapan(c, o)))
            {
                if (!informados.Add((Math.Min(c.Id, o.Id), Math.Max(c.Id, o.Id))))
                    continue;

                if (idsLote.Contains(o.Id))
                {
                    errores.Add($"Las clases {c.NroClase} y {o.NroClase} quedan en el aula {aula.Codigo} con horarios superpuestos " +
                                $"({DiasComunes(c, o)} {c.Horario} / {o.Horario}).");
                    continue;
                }

                var mensaje = $"La clase {c.NroClase} ({NombreMateria(c, materias)}) choca en el aula {aula.Codigo} con la clase " +
                              $"{o.NroClase} ({NombreMateria(o, materias)}, {string.Join("/", o.Dias)} {o.Horario}). " +
                              $"Si es un intercambio, la clase {o.NroClase} tiene que moverse en el mismo lote";
                var aulaQueDeja = inicial[c.Id].AulaId;
                if (aulaQueDeja.HasValue && aulaQueDeja != c.AulaId)
                    mensaje += $" (por ejemplo, al aula {aulas[aulaQueDeja.Value].Codigo}, que deja la clase {c.NroClase})";
                errores.Add(mensaje + ".");
            }
        }

        return errores;
    }

    private static List<string> ValidarChoquesDocente(List<EstadoClase> modificadas, Dictionary<long, EstadoClase> final)
    {
        var errores = new List<string>();
        var informados = new HashSet<(long, long, int)>();

        foreach (var c in modificadas)
        {
            foreach (var docenteId in c.DocenteIds)
            {
                foreach (var o in final.Values.Where(o => o.Id != c.Id && o.DocenteIds.Contains(docenteId) && Solapan(c, o)))
                {
                    if (informados.Add((Math.Min(c.Id, o.Id), Math.Max(c.Id, o.Id), docenteId)))
                        errores.Add($"El docente {docenteId} queda en dos clases a la vez: {c.NroClase} y {o.NroClase} " +
                                    $"({DiasComunes(c, o)} {c.Horario} / {o.Horario}).");
                }
            }
        }

        return errores;
    }

    // ---------------------------------------------------------------- Opciones de aula para una clase

    /// <summary>
    /// Para la pantalla del backoffice: a qué aulas de la sede se puede mover la clase y qué implica cada una
    /// (libre, intercambio directo o rueda), con los cambios ya armados cuando se resuelven solos.
    /// </summary>
    public async Task<IResult> GetOpcionesAulaAsync(long claseId)
    {
        var claseModel = await _repository.GetClaseByIdAsync(claseId);
        if (claseModel == null)
            return Results.NotFound(new { error = $"No existe la clase {claseId}." });

        var (clases, aulas, materias) = await CargarEstadoPeriodoAsync(claseModel.PeriodoId);
        var clase = clases[claseId];
        Aula? aulaActual = clase.AulaId.HasValue ? aulas[clase.AulaId.Value] : null;
        var aulaActualAdmiteIntercambio = aulaActual != null && !AulasSinChoque.Contains(aulaActual.TipoGrupo);

        var response = new OpcionesAulaResponse
        {
            ClaseId = clase.Id,
            NroClase = clase.NroClase,
            Materia = NombreMateria(clase, materias),
            Inscriptos = clase.InscriptosConReserva,
            AulaActualId = aulaActual?.Id,
            AulaActualCodigo = aulaActual?.Codigo,
            AulaActualCapacidad = aulaActual?.CapacidadTotal
        };

        foreach (var aula in aulas.Values.Where(a => a.SedeId == clase.SedeId && a.Activa && a.Id != clase.AulaId
                                                     && !AulasSinChoque.Contains(a.TipoGrupo)))
        {
            var ocupantes = clases.Values.Where(o => o.Id != clase.Id && o.AulaId == aula.Id && Solapan(clase, o)).ToList();

            var opcion = new OpcionAulaDto
            {
                AulaId = aula.Id,
                Codigo = aula.Codigo,
                TipoGrupo = aula.TipoGrupo,
                Piso = aula.Piso,
                Capacidad = aula.CapacidadTotal,
                SillasVacias = aula.CapacidadTotal - clase.InscriptosConReserva,
                EntranTodos = aula.CapacidadTotal >= clase.InscriptosConReserva,
                Ocupantes = ocupantes.Select(o => new OcupanteAulaDto
                {
                    ClaseId = o.Id,
                    NroClase = o.NroClase,
                    Materia = NombreMateria(o, materias),
                    Inscriptos = o.InscriptosConReserva,
                    Dias = o.Dias,
                    Horario = o.Horario,
                    // Puede ir al aula que deja la clase si entra y no choca con otra que ya esté ahí
                    PuedeIrAlAulaActual = aulaActualAdmiteIntercambio
                        && aulaActual!.CapacidadTotal >= o.InscriptosConReserva
                        && !clases.Values.Any(p => p.Id != clase.Id && p.Id != o.Id && p.AulaId == aulaActual.Id && Solapan(o, p))
                }).ToList()
            };

            if (ocupantes.Count == 0)
            {
                opcion.Tipo = TipoOpcionAula.Libre;
                opcion.CambiosSugeridos = new() { new CambioEdicionDto { ClaseId = clase.Id, AulaId = aula.Id } };
            }
            else if (ocupantes.Count == 1 && opcion.Ocupantes[0].PuedeIrAlAulaActual)
            {
                opcion.Tipo = TipoOpcionAula.IntercambioDirecto;
                opcion.CambiosSugeridos = new()
                {
                    new CambioEdicionDto { ClaseId = clase.Id, AulaId = aula.Id },
                    new CambioEdicionDto { ClaseId = ocupantes[0].Id, AulaId = aulaActual!.Id }
                };
            }
            else
            {
                opcion.Tipo = TipoOpcionAula.RequiereRueda;
            }

            response.Opciones.Add(opcion);
        }

        response.Opciones = response.Opciones
            .OrderByDescending(o => o.EntranTodos)
            .ThenBy(o => o.EntranTodos ? o.SillasVacias : -o.SillasVacias)
            .ThenBy(o => o.Tipo == TipoOpcionAula.Libre ? 0 : o.Tipo == TipoOpcionAula.IntercambioDirecto ? 1 : 2)
            .ToList();

        return Results.Ok(response);
    }

    // ---------------------------------------------------------------- Helpers

    private async Task<(Dictionary<long, EstadoClase> Clases, Dictionary<int, Aula> Aulas, Dictionary<string, Materia> Materias)>
        CargarEstadoPeriodoAsync(int periodoId)
    {
        var clases = await _repository.GetClasesAsync(periodoId: periodoId);
        var aulaPorClase = (await _repository.GetAllAsignacionesAsync())
            .GroupBy(a => a.ClaseId)
            .ToDictionary(g => g.Key, g => g.First().AulaId);
        var docentesPorClase = await _repository.GetDocentesPorClaseAsync();
        var aulas = (await _repository.GetAulasAsync()).ToDictionary(a => a.Id);
        var materias = (await _repository.GetMateriasAsync()).ToDictionary(m => m.Codigo);

        var estado = clases.ToDictionary(c => c.Id, c => new EstadoClase
        {
            Id = c.Id,
            NroClase = c.NroClase,
            SedeId = c.SedeId,
            InscriptosConReserva = c.InscriptosConReserva,
            MateriaCodigo = c.MateriaCodigo,
            Turno = c.Turno,
            Dias = c.Dias ?? Array.Empty<string>(),
            Desde = c.HorarioDesde,
            Hasta = c.HorarioHasta,
            AulaId = aulaPorClase.GetValueOrDefault(c.Id),
            DocenteIds = docentesPorClase.TryGetValue(c.Id, out var ds) ? ds.Select(d => d.Id).ToList() : new()
        });

        return (estado, aulas, materias);
    }

    private static bool Solapan(EstadoClase a, EstadoClase b) =>
        a.Dias.Intersect(b.Dias).Any() && a.Desde < b.Hasta && b.Desde < a.Hasta;

    private static string DiasComunes(EstadoClase a, EstadoClase b) => string.Join("/", a.Dias.Intersect(b.Dias));

    private static string NombreMateria(EstadoClase c, Dictionary<string, Materia> materias) =>
        materias.TryGetValue(c.MateriaCodigo, out var m) ? m.Nombre : c.MateriaCodigo;

    private static bool MismosDocentes(EstadoClase a, EstadoClase b) =>
        a.DocenteIds.OrderBy(x => x).SequenceEqual(b.DocenteIds.OrderBy(x => x));

    private static bool Difiere(EstadoClase a, EstadoClase b) =>
        a.MateriaCodigo != b.MateriaCodigo || a.Turno != b.Turno || !a.Dias.SequenceEqual(b.Dias) ||
        a.Desde != b.Desde || a.Hasta != b.Hasta || a.AulaId != b.AulaId || !MismosDocentes(a, b);

    /// <summary>Solo los campos que cambiaron, para la auditoría.</summary>
    private static (Dictionary<string, object?> Antes, Dictionary<string, object?> Despues) Diferencias(
        EstadoClase a, EstadoClase b, Dictionary<int, Aula> aulas)
    {
        var antes = new Dictionary<string, object?>();
        var despues = new Dictionary<string, object?>();
        void Campo(string nombre, object? va, object? vb) { antes[nombre] = va; despues[nombre] = vb; }

        if (a.MateriaCodigo != b.MateriaCodigo) Campo("materia", a.MateriaCodigo, b.MateriaCodigo);
        if (a.Turno != b.Turno) Campo("turno", a.Turno, b.Turno);
        if (!a.Dias.SequenceEqual(b.Dias)) Campo("dias", a.Dias, b.Dias);
        if (a.Desde != b.Desde || a.Hasta != b.Hasta) Campo("horario", a.Horario, b.Horario);
        if (a.AulaId != b.AulaId)
            Campo("aula",
                a.AulaId.HasValue ? aulas[a.AulaId.Value].Codigo : null,
                b.AulaId.HasValue ? aulas[b.AulaId.Value].Codigo : null);
        if (!MismosDocentes(a, b)) Campo("docenteIds", a.DocenteIds, b.DocenteIds);

        return (antes, despues);
    }

    private static bool TryParseHora(string texto, out TimeOnly hora) =>
        TimeOnly.TryParse(texto.Trim().Replace('.', ':'), CultureInfo.InvariantCulture, out hora);
}
