using UadeAulas.Api.Dtos;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

/// <summary>Demanda provisoria (pestaña "Demanda"): Excel guardado y totales por columna con filtros.</summary>
public static class DemandaExcelEndpoints
{
    public static void MapDemandaExcelEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/demanda-excel").WithTags("DemandaExcel");

        group.MapGet("/", GetInfo).WithName("GetDemandaExcel");
        group.MapPost("/", Subir).WithName("SubirDemandaExcel").DisableAntiforgery();
        group.MapPost("/valores", GetValores).WithName("GetValoresDemandaExcel");
        group.MapPost("/totales", GetTotales).WithName("GetTotalesDemandaExcel");
    }

    public static async Task<IResult> GetInfo(DemandaExcelService service) =>
        Results.Ok(await service.GetInfoAsync());

    public static async Task<IResult> Subir(HttpRequest request, DemandaExcelService service)
    {
        var (excel, archivo, error) = await ExcelSubido.LeerAsync(request);
        if (error != null) return error;

        var usuario = UsuarioActual.Leer(request) ?? UsuarioActual.SinIdentificar;
        var filas = await service.GuardarAsync(archivo!, usuario, excel!);
        return Results.Ok(new { exito = true, archivo, filas });
    }

    public static async Task<IResult> GetValores(ValoresColumnaRequest request, DemandaExcelService service)
    {
        try
        {
            var valores = await service.GetValoresAsync(request);
            return valores == null ? SinDemanda() : Results.Ok(valores);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.BadRequest(new { error = ex.Message.Split(" (Parameter")[0] });
        }
    }

    public static async Task<IResult> GetTotales(TotalesDemandaRequest request, DemandaExcelService service)
    {
        try
        {
            var totales = await service.GetTotalesAsync(request);
            return totales == null ? SinDemanda() : Results.Ok(totales);
        }
        catch (SinFiltrosException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Results.BadRequest(new { error = ex.Message.Split(" (Parameter")[0] });
        }
    }

    private static IResult SinDemanda() => Results.NotFound(new { error = "Todavía no se subió el Excel de demanda." });
}
