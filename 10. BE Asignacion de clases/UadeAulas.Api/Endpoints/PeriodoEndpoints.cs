using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;
using UadeAulas.Api.Models;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

public static class PeriodoEndpoints
{
    public static void MapPeriodoEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/periodos")
            .WithTags("Periodos");

        group.MapGet("/", GetPeriodos)
            .WithName("GetPeriodos")
            .Produces<List<Periodo>>();

        group.MapGet("/actual", GetPeriodoActual)
            .WithName("GetPeriodoActual")
            .Produces<Periodo>()
            .Produces(404);

        group.MapGet("/{id:int}", GetPeriodo)
            .WithName("GetPeriodo")
            .Produces<Periodo>()
            .Produces(404);

        group.MapPut("/{id:int}/fechas", ActualizarFechas)
            .WithName("ActualizarFechasPlanificacion")
            .Produces<Periodo>()
            .Produces(400)
            .Produces(404)
            .Produces(409);

        group.MapPost("/{id:int}/copiar", CopiarPlanificacion)
            .WithName("CopiarPlanificacion")
            .Produces<CopiaPlanificacionResultDto>()
            .Produces<CopiaPlanificacionResultDto>(400)
            .Produces(404)
            .Produces<CopiaPlanificacionResultDto>(409);

        group.MapPost("/{id:int}/reabrir", Reabrir)
            .WithName("ReabrirPlanificacion")
            .Produces<Periodo>()
            .Produces(400)
            .Produces(404)
            .Produces(409);

        group.MapPost("/{id:int}/cerrar-reapertura", CerrarReapertura)
            .WithName("CerrarReaperturaPlanificacion")
            .Produces<Periodo>()
            .Produces(404)
            .Produces(409);
    }

    /// <summary>Reabre por excepción una planificación finalizada {motivo} (header X-Usuario obligatorio).</summary>
    public static async Task<IResult> Reabrir(int id, MotivoDto dto, HttpRequest request, PeriodoService service)
    {
        var usuario = UsuarioActual.Leer(request);
        if (usuario == null)
            return UsuarioActual.FaltaUsuario();

        return await service.ReabrirAsync(id, dto, usuario);
    }

    /// <summary>Cierra la reapertura: la planificación vuelve a quedar finalizada (header X-Usuario obligatorio).</summary>
    public static async Task<IResult> CerrarReapertura(int id, HttpRequest request, PeriodoService service)
    {
        var usuario = UsuarioActual.Leer(request);
        if (usuario == null)
            return UsuarioActual.FaltaUsuario();

        return await service.CerrarReaperturaAsync(id, usuario);
    }

    /// <summary>
    /// Copia la planificación finalizada {id} a otro cuatrimestre {anio, cuatrimestre} (header X-Usuario obligatorio).
    /// </summary>
    public static async Task<IResult> CopiarPlanificacion(int id, CopiarPlanificacionRequest dto, HttpRequest request, PeriodoService service)
    {
        var usuario = UsuarioActual.Leer(request);
        if (usuario == null)
            return UsuarioActual.FaltaUsuario();

        return await service.CopiarPlanificacionAsync(id, dto, usuario);
    }

    /// <summary>
    /// El backoffice carga la fecha de congelamiento y la de fin de inscripción (header X-Usuario obligatorio).
    /// </summary>
    public static async Task<IResult> ActualizarFechas(int id, FechasPlanificacionDto dto, HttpRequest request, PeriodoService service)
    {
        var usuario = UsuarioActual.Leer(request);
        if (usuario == null)
            return UsuarioActual.FaltaUsuario();

        return await service.ActualizarFechasAsync(id, dto, usuario);
    }

    /// <summary>
    /// Un período con su planificación: fechas de congelamiento/inscripción y estado calculado.
    /// </summary>
    public static async Task<IResult> GetPeriodo(int id, Repository repository)
    {
        var periodo = await repository.GetPeriodoByIdAsync(id);
        return periodo == null
            ? Results.NotFound(new { error = $"No existe el período {id}." })
            : Results.Ok(periodo);
    }

    public static async Task<IResult> GetPeriodos(Repository repository)
    {
        return Results.Ok(await repository.GetPeriodosAsync());
    }

    /// <summary>
    /// Período al que corresponde la información que muestran oferta, demanda y mapa.
    /// </summary>
    public static async Task<IResult> GetPeriodoActual(Repository repository)
    {
        var periodo = await repository.GetPeriodoActualAsync();
        return periodo == null
            ? Results.NotFound(new { error = "No hay períodos cargados." })
            : Results.Ok(periodo);
    }
}
