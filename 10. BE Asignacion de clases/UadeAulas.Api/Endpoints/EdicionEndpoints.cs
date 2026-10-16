using UadeAulas.Api.Dtos;
using UadeAulas.Api.Services;

namespace UadeAulas.Api.Endpoints;

public static class EdicionEndpoints
{
    public static void MapEdicionEndpoints(this WebApplication app)
    {
        app.MapPost("/periodos/{id:int}/cambios", AplicarLote)
            .WithTags("Edicion")
            .WithName("AplicarLoteCambios")
            .Produces<LoteCambiosResponse>(200)
            .Produces<LoteCambiosResponse>(400)
            .Produces(404)
            .Produces(409);

        app.MapGet("/clases/{id:long}/opciones-aula", GetOpcionesAula)
            .WithTags("Edicion")
            .WithName("GetOpcionesAula")
            .Produces<OpcionesAulaResponse>()
            .Produces(404);
    }

    /// <summary>
    /// Aplica un lote de cambios de clases del período (todo o nada). Header X-Usuario obligatorio.
    /// </summary>
    public static async Task<IResult> AplicarLote(int id, LoteCambiosRequest request, HttpRequest http, EdicionPlanificacionService service)
    {
        var usuario = UsuarioActual.Leer(http);
        if (usuario == null)
            return UsuarioActual.FaltaUsuario();

        return await service.AplicarLoteAsync(id, request, usuario);
    }

    public static Task<IResult> GetOpcionesAula(long id, EdicionPlanificacionService service) =>
        service.GetOpcionesAulaAsync(id);
}
