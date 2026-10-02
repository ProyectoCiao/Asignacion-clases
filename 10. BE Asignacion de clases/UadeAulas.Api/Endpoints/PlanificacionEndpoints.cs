using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

public static class PlanificacionEndpoints
{
    public static void MapPlanificacionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/planificacion")
            .WithTags("Planificacion");

        group.MapPost("/cargar", CargarPlanificacion)
            .WithName("CargarPlanificacion")
            .DisableAntiforgery()
            .Produces(200)
            .Produces(400);
    }

    private const long MaxArchivoBytes = 10 * 1024 * 1024; // 10 MB

    public static async Task<IResult> CargarPlanificacion(HttpRequest request, PlanificacionService service)
    {
        if (!request.HasFormContentType)
            return Results.BadRequest(new { error = "Se esperaba un formulario multipart/form-data con el archivo Excel." });

        var form = await request.ReadFormAsync();

        var anio = form["anio"].FirstOrDefault() ?? form["año"].FirstOrDefault() ?? form["year"].FirstOrDefault();
        var cuatrimestre = form["cuatrimestre"].FirstOrDefault() ?? form["cuatri"].FirstOrDefault() ?? form["periodo"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(anio) || string.IsNullOrWhiteSpace(cuatrimestre))
            return Results.BadRequest(new { error = "Faltan los campos 'cuatrimestre' y/o 'anio'." });

        var archivo = form.Files["archivo"] ?? form.Files["archivoExcel"] ?? form.Files["file"] ?? form.Files["excel"]
            ?? form.Files.FirstOrDefault();

        if (archivo == null || archivo.Length == 0)
            return Results.BadRequest(new { error = "No se recibió ningún archivo Excel." });

        if (!archivo.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            return Results.BadRequest(new { error = "El archivo debe tener extensión .xlsx." });

        if (archivo.Length > MaxArchivoBytes)
            return Results.BadRequest(new { error = "El archivo supera el tamaño máximo permitido (10 MB)." });

        try
        {
            using var stream = archivo.OpenReadStream();
            var resultado = await service.CargarPlanificacionAsync(stream, cuatrimestre, anio);
            return resultado.Exito ? Results.Ok(resultado) : Results.BadRequest(resultado);
        }
        catch (Exception ex)
        {
            // Errores de lectura del archivo (Excel corrupto o con formato inesperado)
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
