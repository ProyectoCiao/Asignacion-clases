using UadeAulas.Api.Data;

namespace UadeAulas.Api.Endpoints;

/// <summary>
/// Opciones de los desplegables: GET /maestros/sedes, /turnos, /anios, /edificios, /tipos-aula, /facultades, /materias,
/// /pisos y /aulas. Los filtros aceptan varios valores repitiendo el parámetro (?sede=1&amp;sede=3).
/// </summary>
public static class TablasMaestrasEndpoints
{
    public static void MapTablasMaestrasEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/maestros").WithTags("Maestros");

        group.MapGet("/sedes", async (TablasMaestrasRepository repo) => Results.Ok(await repo.GetSedesAsync()))
            .WithName("GetMaestroSedes");
        group.MapGet("/turnos", async (TablasMaestrasRepository repo) => Results.Ok(await repo.GetTurnosAsync()))
            .WithName("GetMaestroTurnos");
        group.MapGet("/periodos", async (TablasMaestrasRepository repo) => Results.Ok(await repo.GetPeriodosAsync()))
            .WithName("GetMaestroPeriodos");
        group.MapGet("/anios", async (TablasMaestrasRepository repo) => Results.Ok(await repo.GetAniosAsync()))
            .WithName("GetMaestroAnios");
        // ?sede=1&sede=3 → edificios con aulas en esas sedes (aulas.edificio_id); sin sede, todos los que tienen aulas
        group.MapGet("/edificios", async (TablasMaestrasRepository repo, int[]? sede) =>
                Results.Ok(await repo.GetEdificiosAsync(sede)))
            .WithName("GetMaestroEdificios");
        // ?sede=1 → tipos de aula (tabla aula_tipo) con aulas en esas sedes; sin sede, los que tienen alguna aula
        group.MapGet("/tipos-aula", async (TablasMaestrasRepository repo, int[]? sede) =>
                Results.Ok(await repo.GetTiposAulaAsync(sede)))
            .WithName("GetMaestroTiposAula");
        group.MapGet("/facultades", async (TablasMaestrasRepository repo) => Results.Ok(await repo.GetFacultadesAsync()))
            .WithName("GetMaestroFacultades");
        // ?facultad=FACE&facultad=FAIN&sede=1 → materias de esas facultades (materia_facultad) y de esas sedes (materia_sede)
        group.MapGet("/materias", async (TablasMaestrasRepository repo, string[]? facultad, int[]? sede) =>
                Results.Ok(await repo.GetMateriasAsync(
                    facultad?.Where(f => !string.IsNullOrWhiteSpace(f)).ToArray(), sede)))
            .WithName("GetMaestroMaterias");
        // ?sede=1&edificio=4 → pisos de las aulas de esas sedes y edificios: tabla aulas, columna aula_piso
        group.MapGet("/pisos", async (TablasMaestrasRepository repo, int[]? sede, int[]? edificio) =>
                Results.Ok(await repo.GetPisosAsync(sede, edificio)))
            .WithName("GetMaestroPisos");
        // ?sede=1&piso=3&edificio=9&tipo=8 (ids de sedes, edificios y aula_tipo) → aulas filtradas; sin filtros, todas
        group.MapGet("/aulas", async (TablasMaestrasRepository repo, int[]? sede, string[]? piso, int[]? edificio, int[]? tipo) =>
                Results.Ok(await repo.GetAulasAsync(
                    sede, piso?.Select(p => p.Trim()).Where(p => p.Length > 0).ToArray(), edificio, tipo)))
            .WithName("GetMaestroAulas");
    }
}
