using UadeAulas.Api.Data;
using UadeAulas.Api.Models;

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
