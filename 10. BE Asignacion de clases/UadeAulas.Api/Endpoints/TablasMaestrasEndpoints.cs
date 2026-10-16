using UadeAulas.Api.Data;

namespace UadeAulas.Api.Endpoints;

/// <summary>Opciones de los desplegables: GET /maestros/sedes, /turnos, /edificios, /tipos-aula, /facultades, /materias, /pisos y /aulas.</summary>
public static class TablasMaestrasEndpoints
{
    public static void MapTablasMaestrasEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/maestros").WithTags("Maestros");

        group.MapGet("/sedes", async (TablasMaestrasRepository repo) => Results.Ok(await repo.GetSedesAsync()))
            .WithName("GetMaestroSedes");
        group.MapGet("/turnos", async (TablasMaestrasRepository repo) => Results.Ok(await repo.GetTurnosAsync()))
            .WithName("GetMaestroTurnos");
        // ?sede=1 → edificios con aulas en esa sede (aulas.edificio_id); sin sede, todos
        group.MapGet("/edificios", async (TablasMaestrasRepository repo, int? sede) =>
                Results.Ok(await repo.GetEdificiosAsync(sede)))
            .WithName("GetMaestroEdificios");
        // ?sede=1 → tipos de aula (tabla aula_tipo) con aulas en esa sede; sin sede, los que tienen alguna aula
        group.MapGet("/tipos-aula", async (TablasMaestrasRepository repo, int? sede) =>
                Results.Ok(await repo.GetTiposAulaAsync(sede)))
            .WithName("GetMaestroTiposAula");
        group.MapGet("/facultades", async (TablasMaestrasRepository repo) => Results.Ok(await repo.GetFacultadesAsync()))
            .WithName("GetMaestroFacultades");
        // ?facultad=FACE&sede=1 → solo las materias de esa facultad (materia_facultad) y de esa sede (materia_sede)
        group.MapGet("/materias", async (TablasMaestrasRepository repo, string? facultad, int? sede) =>
                Results.Ok(await repo.GetMateriasAsync(string.IsNullOrWhiteSpace(facultad) ? null : facultad, sede)))
            .WithName("GetMaestroMaterias");
        // ?sede=1&edificio=4 → pisos de las aulas de esa sede (y de ese edificio si se indica): tabla aulas, columna aula_piso
        group.MapGet("/pisos", async (TablasMaestrasRepository repo, int sede, int? edificio) =>
                Results.Ok(await repo.GetPisosAsync(sede, edificio)))
            .WithName("GetMaestroPisos");
        // ?sede=1&piso=3&edificio=9&tipo=8 (ids de sedes, edificios y aula_tipo) → aulas filtradas; sin filtros, todas
        group.MapGet("/aulas", async (TablasMaestrasRepository repo, int? sede, string? piso, int? edificio, int? tipo) =>
                Results.Ok(await repo.GetAulasAsync(sede,
                    string.IsNullOrWhiteSpace(piso) ? null : piso.Trim(),
                    edificio,
                    tipo)))
            .WithName("GetMaestroAulas");
    }
}
