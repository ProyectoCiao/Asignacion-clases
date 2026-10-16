namespace UadeAulas.Api.Models;

/// <summary>
/// Un registro de auditoria_cambios. Antes/Despues se serializan a jsonb.
/// </summary>
public class Auditoria
{
    public string Usuario { get; set; } = string.Empty;
    public int? PeriodoId { get; set; }
    public string Entidad { get; set; } = string.Empty;
    public long? EntidadId { get; set; }
    public string Accion { get; set; } = AccionAuditoria.Modificacion;
    public bool EsEmergencia { get; set; }
    public string? Motivo { get; set; }
    public object? Antes { get; set; }
    public object? Despues { get; set; }
    /// <summary>Agrupa los registros de cambios aplicados juntos en una misma operación.</summary>
    public Guid? Lote { get; set; }
}

public static class AccionAuditoria
{
    public const string Alta = "ALTA";
    public const string Modificacion = "MODIFICACION";
    public const string Baja = "BAJA";
}
