using UadeAulas.Api.Dtos;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

public static class OfertaEndpoints
{
    public static void MapOfertaEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/oferta")
            .WithTags("Oferta");

        group.MapGet("/", GetOferta)
            .WithName("GetOferta")
            .Produces<List<OfertaRowDto>>();

        group.MapGet("/catalogos", GetCatalogos)
            .WithName("GetCatalogos")
            .Produces<CatalogosDto>();

        group.MapPost("/grabar", GrabarCambios)
            .WithName("GrabarCambios")
            .Produces<GrabarResponse>(200)
            .Produces(400);
    }

    public static async Task<IResult> GetOferta(
        OfertaService service,
        int? sede = null,
        string? turno = null,
        string? q = null,
        string? facultad = null)
    {
        var filas = await service.GetOfertaAsync(sede, turno, q);

        // Filtrar por facultad si se proporciona
        if (!string.IsNullOrEmpty(facultad))
            filas = filas.Where(f => f.Facultad == facultad).ToList();

        return Results.Ok(filas);
    }

    public static async Task<IResult> GetCatalogos(OfertaService service)
    {
        var catalogos = await service.GetCatalogosAsync();
        return Results.Ok(catalogos);
    }

    public static async Task<IResult> GrabarCambios(GrabarService service, GrabarRequest request)
    {
        try
        {
            var response = await service.GrabarCambiosAsync(request);
            return Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}
