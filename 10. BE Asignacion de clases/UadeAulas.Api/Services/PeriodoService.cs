using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;
using UadeAulas.Api.Models;

namespace UadeAulas.Api.Services;

public class PeriodoService
{
    private readonly Repository _repository;

    public PeriodoService(Repository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Carga las fechas de congelamiento y fin de inscripción de la planificación de un período.
    /// - Finalizada o reabierta: no se pueden modificar (409). Mover el fin de inscripción reabriría la inscripción.
    /// - Inscripción abierta: se permite como cambio de emergencia, con motivo obligatorio.
    /// - El fin de inscripción tiene que ser futuro: al llegar esa fecha la planificación se finaliza sola.
    /// Devuelve el período actualizado, o un IResult de error.
    /// </summary>
    public async Task<IResult> ActualizarFechasAsync(int periodoId, FechasPlanificacionDto dto, string usuario)
    {
        var periodo = await _repository.GetPeriodoByIdAsync(periodoId);
        if (periodo == null)
            return Results.NotFound(new { error = $"No existe el período {periodoId}." });

        var estado = periodo.Estado;
        if (estado is EstadoPlanificacion.Finalizada or EstadoPlanificacion.Reabierta)
            return Results.Conflict(new { error = $"El {periodo.Nombre} ya cerró la inscripción ({estado}): sus fechas no se pueden modificar." });

        var congelamiento = dto.FechaCongelamiento?.UtcDateTime;
        var finInscripcion = dto.FechaFinInscripcion?.UtcDateTime;

        if (finInscripcion.HasValue && !congelamiento.HasValue)
            return Results.BadRequest(new { error = "No se puede definir el fin de inscripción sin la fecha de congelamiento." });
        if (congelamiento.HasValue && finInscripcion.HasValue && finInscripcion <= congelamiento)
            return Results.BadRequest(new { error = "La fecha de fin de inscripción debe ser posterior a la de congelamiento." });
        if (finInscripcion.HasValue && finInscripcion <= DateTime.UtcNow)
            return Results.BadRequest(new { error = "La fecha de fin de inscripción tiene que ser futura: al llegar, la planificación se finaliza automáticamente." });

        var motivo = string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim();
        var esEmergencia = estado != EstadoPlanificacion.EnEdicion;
        if (esEmergencia && motivo == null)
            return Results.BadRequest(new
            {
                error = $"La planificación ya está congelada ({estado}): el cambio de fechas es de emergencia y requiere un motivo."
            });

        var auditoria = new Auditoria
        {
            Usuario = usuario,
            PeriodoId = periodo.Id,
            Entidad = "periodo",
            EntidadId = periodo.Id,
            Accion = AccionAuditoria.Modificacion,
            EsEmergencia = esEmergencia,
            Motivo = motivo,
            Antes = new { fechaCongelamiento = periodo.FechaCongelamiento, fechaFinInscripcion = periodo.FechaFinInscripcion, estado },
            Despues = new { fechaCongelamiento = congelamiento, fechaFinInscripcion = finInscripcion }
        };

        await _repository.UpdateFechasPlanificacionAsync(periodo.Id, congelamiento, finInscripcion, auditoria);
        return Results.Ok(await _repository.GetPeriodoByIdAsync(periodo.Id));
    }

    /// <summary>
    /// Excepción: reabre una planificación FINALIZADA (ej. aula en refacción o sin proyector) para poder
    /// editarla con motivo. Queda REABIERTA hasta que se cierre con CerrarReaperturaAsync.
    /// </summary>
    public async Task<IResult> ReabrirAsync(int periodoId, MotivoDto dto, string usuario)
    {
        var periodo = await _repository.GetPeriodoByIdAsync(periodoId);
        if (periodo == null)
            return Results.NotFound(new { error = $"No existe el período {periodoId}." });

        var estado = periodo.Estado;
        if (estado != EstadoPlanificacion.Finalizada)
            return Results.Conflict(new { error = $"Solo se puede reabrir una planificación finalizada (el {periodo.Nombre} está {estado})." });

        var motivo = string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim();
        if (motivo == null)
            return Results.BadRequest(new { error = "Reabrir una planificación finalizada es una excepción: el motivo es obligatorio." });

        await _repository.SetReaperturaAsync(periodo.Id, reabrir: true, usuario, motivo, new Auditoria
        {
            Usuario = usuario,
            PeriodoId = periodo.Id,
            Entidad = "periodo",
            EntidadId = periodo.Id,
            Accion = AccionAuditoria.Modificacion,
            EsEmergencia = true,
            Motivo = motivo,
            Antes = new { estado },
            Despues = new { estado = EstadoPlanificacion.Reabierta }
        });
        return Results.Ok(await _repository.GetPeriodoByIdAsync(periodo.Id));
    }

    /// <summary>Cierra la reapertura vigente: la planificación vuelve a FINALIZADA.</summary>
    public async Task<IResult> CerrarReaperturaAsync(int periodoId, string usuario)
    {
        var periodo = await _repository.GetPeriodoByIdAsync(periodoId);
        if (periodo == null)
            return Results.NotFound(new { error = $"No existe el período {periodoId}." });

        var estado = periodo.Estado;
        if (estado != EstadoPlanificacion.Reabierta)
            return Results.Conflict(new { error = $"El {periodo.Nombre} no está reabierto (está {estado})." });

        await _repository.SetReaperturaAsync(periodo.Id, reabrir: false, null, null, new Auditoria
        {
            Usuario = usuario,
            PeriodoId = periodo.Id,
            Entidad = "periodo",
            EntidadId = periodo.Id,
            Accion = AccionAuditoria.Modificacion,
            Antes = new { estado, reabiertaEn = periodo.ReabiertaEn, reabiertaPor = periodo.ReabiertaPor, motivoReapertura = periodo.ReabiertaMotivo },
            Despues = new { estado = EstadoPlanificacion.Finalizada }
        });
        return Results.Ok(await _repository.GetPeriodoByIdAsync(periodo.Id));
    }

    /// <summary>
    /// Arranca la planificación de otro cuatrimestre copiando una FINALIZADA: clases, docentes y aulas, sin
    /// inscriptos. El destino queda EN_EDICION (sin fechas), con origen COPIA. Si el período destino ya tiene
    /// planificación, no se copia (409).
    /// </summary>
    public async Task<IResult> CopiarPlanificacionAsync(int periodoOrigenId, CopiarPlanificacionRequest request, string usuario)
    {
        var resultado = new CopiaPlanificacionResultDto();

        var origen = await _repository.GetPeriodoByIdAsync(periodoOrigenId);
        if (origen == null)
            return Results.NotFound(new { error = $"No existe el período {periodoOrigenId}." });
        resultado.PeriodoOrigen = origen.Nombre;

        if (origen.Estado != EstadoPlanificacion.Finalizada)
            return Rechazar(resultado, StatusCodes.Status409Conflict,
                $"Solo se puede copiar una planificación finalizada (el {origen.Nombre} está {origen.Estado}).");

        if (request.Anio is < 2000 or > 2100 || request.Cuatrimestre is not (1 or 2))
            return Rechazar(resultado, StatusCodes.Status400BadRequest, "Año o cuatrimestre inválidos (cuatrimestre: 1 o 2).");

        var ciclo = $"{request.Anio}-{request.Cuatrimestre}";
        if (ciclo == origen.Ciclo)
            return Rechazar(resultado, StatusCodes.Status400BadRequest, "El período destino no puede ser el mismo que el origen.");

        var destino = await _repository.GetPeriodoByCicloAsync(ciclo);
        if (destino != null && (destino.Estado == EstadoPlanificacion.Finalizada || await _repository.PeriodoTieneClasesAsync(destino.Id)))
            return Rechazar(resultado, StatusCodes.Status409Conflict, $"El {destino.Nombre} ya tiene planificación.");

        var (nombre, fechaDesde, fechaHasta) = PlanificacionService.DatosPeriodoNuevo(request.Anio, request.Cuatrimestre);
        var copia = await _repository.CopiarPlanificacionAsync(origen.Id, ciclo, nombre, fechaDesde, fechaHasta,
            r => new Auditoria
            {
                Usuario = usuario,
                PeriodoId = r.PeriodoId,
                Entidad = "periodo",
                EntidadId = r.PeriodoId,
                Accion = AccionAuditoria.Alta,
                Despues = new
                {
                    origen = OrigenPlanificacion.Copia,
                    periodoOrigenId = origen.Id,
                    clases = r.Clases,
                    asignaciones = r.Asignaciones,
                    docentes = r.Docentes
                }
            });

        // Re-chequeado dentro de la transacción por si otra operación se adelantó
        if (copia == null)
            return Rechazar(resultado, StatusCodes.Status409Conflict, $"El período {ciclo} ya tiene planificación.");

        resultado.Exito = true;
        resultado.PeriodoId = copia.PeriodoId;
        resultado.Periodo = destino?.Nombre ?? nombre;
        resultado.ClasesCopiadas = copia.Clases;
        resultado.AsignacionesCopiadas = copia.Asignaciones;
        resultado.DocentesCopiados = copia.Docentes;
        resultado.Advertencias = copia.ClasesEnAulasInactivas;
        return Results.Ok(resultado);
    }

    private static IResult Rechazar(CopiaPlanificacionResultDto resultado, int status, string error)
    {
        resultado.Error = error;
        return Results.Json(resultado, statusCode: status);
    }
}
