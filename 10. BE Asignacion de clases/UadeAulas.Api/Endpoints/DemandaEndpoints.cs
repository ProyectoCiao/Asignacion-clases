using UadeAulas.Api.Dtos;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

public static class DemandaEndpoints
{
    public static void MapDemandaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/demanda")
            .WithTags("Demanda");

        group.MapGet("/", GetDemanda)
            .WithName("GetDemanda")
            .Produces<List<DemandaRowDto>>();

        group.MapPost("/actualizar", ActualizarDemanda)
            .WithName("ActualizarDemanda")
            .Produces<ActualizarDemandaResponse>(200);
    }

    public static async Task<IResult> GetDemanda(DemandaService service)
    {
        var demandas = await service.GetDemandaAsync();
        return Results.Ok(demandas);
    }

    public static async Task<IResult> ActualizarDemanda(DemandaService service)
    {
        var demandas = await service.ActualizarDemandaAsync();
        return Results.Ok(new ActualizarDemandaResponse { Demandas = demandas });
    }
}
