using UadeAulas.Api.Dtos;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

public static class CatalogEndpoints
{
    public static void MapCatalogEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/")
            .WithTags("Catálogos");

        group.MapGet("/aulas", GetAulas)
            .WithName("GetAulas")
            .Produces<List<AulaDto>>();

        group.MapGet("/materias", GetMaterias)
            .WithName("GetMaterias")
            .Produces<List<MateriaDto>>();

        group.MapGet("/docentes", GetDocentes)
            .WithName("GetDocentes")
            .Produces<List<DocenteDto>>();
    }

    public static async Task<IResult> GetAulas(CatalogService service, int? sedeId = null)
    {
        var aulas = await service.GetAulasAsync(sedeId);
        return Results.Ok(aulas);
    }

    public static async Task<IResult> GetMaterias(CatalogService service)
    {
        var materias = await service.GetMateriasAsync();
        return Results.Ok(materias);
    }

    public static async Task<IResult> GetDocentes(CatalogService service)
    {
        var docentes = await service.GetDocentesAsync();
        return Results.Ok(docentes);
    }
}
