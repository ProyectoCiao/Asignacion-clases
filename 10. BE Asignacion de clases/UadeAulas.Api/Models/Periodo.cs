namespace UadeAulas.Api.Models;

/// <summary>
/// Período (cuatrimestre). Su fila es también la planificación de ese cuatrimestre: una sola, con todas las sedes y turnos.
/// </summary>
public class Periodo
{
    public int Id { get; set; }
    public string Ciclo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public DateTime FechaDesde { get; set; }
    public DateTime FechaHasta { get; set; }

    // Ciclo de vida de la planificación (fechas en UTC; las carga el backoffice)
    public DateTime? FechaCongelamiento { get; set; }
    public DateTime? FechaFinInscripcion { get; set; }
    // Reapertura vigente (excepción manual sobre una planificación finalizada); el historial está en auditoría
    public DateTime? ReabiertaEn { get; set; }
    public string? ReabiertaPor { get; set; }
    public string? ReabiertaMotivo { get; set; }
    public string Origen { get; set; } = OrigenPlanificacion.Excel;
    public int? PeriodoOrigenId { get; set; }

    /// <summary>Estado actual, calculado a partir de las fechas (no se guarda en la base).</summary>
    public string Estado => CalcularEstado(DateTime.UtcNow);

    public string CalcularEstado(DateTime ahoraUtc)
    {
        if (!FechaCongelamiento.HasValue || ahoraUtc < FechaCongelamiento.Value)
            return EstadoPlanificacion.EnEdicion;
        if (!FechaFinInscripcion.HasValue || ahoraUtc < FechaFinInscripcion.Value)
            return EstadoPlanificacion.InscripcionAbierta;
        return ReabiertaEn.HasValue ? EstadoPlanificacion.Reabierta : EstadoPlanificacion.Finalizada;
    }
}

public static class EstadoPlanificacion
{
    /// <summary>Antes del congelamiento: el backoffice edita libremente.</summary>
    public const string EnEdicion = "EN_EDICION";
    /// <summary>Publicada para inscripción: se edita con motivo; la IA sugiere reacomodamientos con la demanda.</summary>
    public const string InscripcionAbierta = "INSCRIPCION_ABIERTA";
    /// <summary>Finalizada automáticamente al cerrar la inscripción: solo lectura, base del próximo cuatrimestre.</summary>
    public const string Finalizada = "FINALIZADA";
    /// <summary>Excepción: finalizada y reabierta a mano (ej. aula en refacción); se edita con motivo hasta cerrarla.</summary>
    public const string Reabierta = "REABIERTA";
}

public static class OrigenPlanificacion
{
    public const string Excel = "EXCEL";
    public const string Copia = "COPIA";
}
