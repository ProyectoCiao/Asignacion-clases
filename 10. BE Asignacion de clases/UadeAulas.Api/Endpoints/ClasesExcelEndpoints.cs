using UadeAulas.Api.Dtos;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

/// <summary>
/// Clases = filas de la planificación guardada (clave "Id" del Excel), con el aula efectiva y la demanda.
/// Cambiar el aula no toca el Excel: se guarda en asignacion_aula. La inscripción de un alumno se agrega al Excel de demanda.
/// </summary>
public static class ClasesExcelEndpoints
{
    private const string HeaderUsuarioReal = "X-Usuario-Real";

    public static void MapClasesExcelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/clases").WithTags("Clases");
        group.MapGet("/", GetPagina).WithName("GetClases");
        group.MapGet("/{id}", GetClase).WithName("GetClase");
        group.MapPut("/{id}/aula", CambiarAula).WithName("CambiarAulaClase");
        group.MapDelete("/{id}/aula", RestaurarAula).WithName("RestaurarAulaClase");

        app.MapPost("/demanda-excel/inscripcion", Inscribir).WithTags("DemandaExcel").WithName("InscribirAlumno");
    }

    /// <summary>sede, turno, periodo ("1er Cuatr", "2do Cuatr"...), edificio, piso y aula aceptan varios valores repitiendo el parámetro.</summary>
    public static async Task<IResult> GetPagina(
        ClasesExcelService service, int? pagina, int? tamanio, string[]? sede, string[]? turno, int[]? edificio,
        string[]? piso, int[]? aula, string? q, bool? soloConAula, string[]? periodo)
    {
        var resultado = await service.GetPaginaAsync(
            pagina ?? 1, tamanio ?? 200, sede, turno, edificio, piso, aula, q, soloConAula, periodo);
        return Results.Ok(resultado);
    }

    public static async Task<IResult> GetClase(string id, ClasesExcelService service)
    {
        var clase = await service.GetClaseAsync(id);
        return clase == null ? Results.NotFound(new { error = $"No existe la clase con Id {id}." }) : Results.Ok(clase);
    }

    public static Task<IResult> CambiarAula(string id, CambiarAulaRequest body, HttpRequest request, ClasesExcelService service)
    {
        var usuario = UsuarioActual.Leer(request);
        if (usuario == null) return Task.FromResult(UsuarioActual.FaltaUsuario());
        var real = request.Headers[HeaderUsuarioReal].FirstOrDefault()?.Trim();
        return Ejecutar(async () => Results.Ok(await service.CambiarAulaAsync(
            id, body.AulaId, usuario, string.IsNullOrEmpty(real) ? usuario : real, body.ForzarCupo)));
    }

    public static Task<IResult> RestaurarAula(string id, ClasesExcelService service) =>
        Ejecutar(async () => Results.Ok(await service.RestaurarAulaAsync(id)));

    public static Task<IResult> Inscribir(InscripcionRequest body, ClasesExcelService service) =>
        Ejecutar(async () => Results.Ok(new { exito = true, totalFilas = await service.InscribirAsync(body) }));

    private static async Task<IResult> Ejecutar(Func<Task<IResult>> accion)
    {
        try
        {
            return await accion();
        }
        catch (ReglaNegocioException ex)
        {
            return Results.Json(new { error = ex.Message, conflictos = ex.Conflictos }, statusCode: ex.Estado);
        }
    }
}
