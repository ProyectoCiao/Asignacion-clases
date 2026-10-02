using UadeAulas.Api.Dtos;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

public static class CuadreEndpoints
{
    public static void MapCuadreEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/cuadre")
            .WithTags("Cuadre");

        group.MapPost("/sugerir", SugerirAula)
            .WithName("SugerirAula")
            .Produces<SugerirResponse>(200)
            .Produces(400);
    }

    public static async Task<IResult> SugerirAula(SugerirService service, SugerirRequest request)
    {
        try
        {
            var response = await service.SugerirAulaAsync(request.ClaseId);
            return Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
