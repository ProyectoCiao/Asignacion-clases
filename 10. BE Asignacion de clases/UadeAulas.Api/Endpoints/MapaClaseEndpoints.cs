using UadeAulas.Api.Dtos;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

public static class MapaClaseEndpoints
{
    public static void MapMapaClaseEndpoints(this WebApplication app)
    {
        app.MapGroup("/mapa")
            .WithTags("Mapa")
            .MapGet("/", GetMapa)
            .WithName("GetMapa")
            .Produces<List<MapaDto>>();

        app.MapGroup("/clases")
            .WithTags("Clases")
            .MapPost("/", CrearClase)
            .WithName("CrearClase")
            .Produces<CrearClaseResponse>(201)
            .Produces(400);
    }

    public static async Task<IResult> GetMapa(
        MapaService service,
        int sedeId,
        int piso,
        string? turno = null,
        string? dia = null)
    {
        var mapa = await service.GetMapaAsync(sedeId, piso, turno, dia);
        return Results.Ok(mapa);
    }

    public static async Task<IResult> CrearClase(ClaseService service, CrearClaseRequest request)
    {
        try
        {
            var response = await service.CrearClaseAsync(request);
            return Results.Created($"/clases/{response.ClaseId}", response);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
