using UadeAulas.Api.Dtos;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

/// <summary>
/// Planificación inicial cruda (pestaña "Última planificación").
/// El Excel no se valida: solo se informan las columnas que van a hacer falta a futuro.
/// </summary>
public static class PlanificacionInicialEndpoints
{
    public static void MapPlanificacionInicialEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/planificacion-inicial").WithTags("PlanificacionInicial");

        group.MapGet("/", GetPagina).WithName("GetPlanificacionInicial");
        group.MapPost("/analizar", Analizar).WithName("AnalizarPlanificacionInicial").DisableAntiforgery();
        group.MapPost("/", Subir).WithName("SubirPlanificacionInicial").DisableAntiforgery();
    }

    /// <summary>sede, turno y facultad aceptan varios valores repitiendo el parámetro (?sede=Monserrat&amp;sede=Belgrano).</summary>
    public static async Task<IResult> GetPagina(
        ExcelCrudoService service, int? pagina, int? tamanio, string[]? sede, string[]? turno, string[]? facultad, string? q)
    {
        var resultado = await service.GetPaginaAsync(
            pagina ?? 1, tamanio ?? ExcelCrudoService.MaxFilasPorPagina, sede, turno, facultad, q);
        return Results.Ok(resultado);
    }

    public static async Task<IResult> Analizar(HttpRequest request, ExcelCrudoService service)
    {
        var (excel, archivo, error) = await ExcelSubido.LeerAsync(request);
        if (error != null) return error;

        return Results.Ok(new ExcelLeidoDto
        {
            Archivo = archivo!,
            Encabezados = excel!.Encabezados,
            TotalFilas = excel.Filas.Count,
            Faltantes = ExcelCrudoService.ColumnasFaltantes(excel.Encabezados),
        });
    }

    public static async Task<IResult> Subir(HttpRequest request, ExcelCrudoService service)
    {
        var (excel, archivo, error) = await ExcelSubido.LeerAsync(request);
        if (error != null) return error;

        // Se sube una sola vez: reemplazar una ya cargada requiere confirmarlo explícitamente
        var reemplazar = string.Equals(request.Form["reemplazar"].FirstOrDefault(), "true", StringComparison.OrdinalIgnoreCase);
        if (!reemplazar && await service.HayPlanificacionAsync())
            return Results.Conflict(new { error = "Ya hay una planificación inicial cargada. Confirmá el reemplazo para subir otra." });

        // Año y cuatrimestre son opcionales: identifican el período pero no condicionan la subida
        int? anio = int.TryParse(request.Form["anio"].FirstOrDefault(), out var a) && a is >= 2000 and <= 2100 ? a : null;
        int? cuatrimestre = int.TryParse(request.Form["cuatrimestre"].FirstOrDefault(), out var c) && c is 1 or 2 ? c : null;

        var usuario = UsuarioActual.Leer(request) ?? UsuarioActual.SinIdentificar;
        var filas = await service.GuardarPlanificacionAsync(archivo!, usuario, anio, cuatrimestre, excel!);
        return Results.Ok(new { exito = true, archivo, filas, faltantes = ExcelCrudoService.ColumnasFaltantes(excel!.Encabezados) });
    }

}
