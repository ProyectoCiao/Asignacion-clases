using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;

namespace UadeAulas.Api.Services;

public class DemandaService
{
    private readonly Repository _repository;

    public DemandaService(Repository repository)
    {
        _repository = repository;
    }

    private async Task<List<Models.Clase>> GetClasesPeriodoActualAsync()
    {
        var periodo = await _repository.GetPeriodoActualAsync();
        return periodo == null
            ? new List<Models.Clase>()
            : await _repository.GetClasesAsync(periodoId: periodo.Id);
    }

    /// <summary>
    /// Obtiene demanda por materia: solicitudes vs asientos disponibles
    /// </summary>
    public async Task<List<DemandaRowDto>> GetDemandaAsync()
    {
        var clases = await GetClasesPeriodoActualAsync();
        var materias = await _repository.GetMateriasAsync();
        var demandaSnapshot = await _repository.GetDemandaSnapshotAsync();

        var demandas = new Dictionary<string, DemandaRowDto>();

        // Agrupar clases por materia
        foreach (var materia in materias)
        {
            var clasesMateria = clases.Where(c => c.MateriaCodigo == materia.Codigo).ToList();
            var solicitudes = demandaSnapshot.ContainsKey(materia.Codigo) 
                ? demandaSnapshot[materia.Codigo] 
                : 0;

            var asientos = clasesMateria.Sum(c => c.Inscriptos);
            var brecha = solicitudes - asientos;

            demandas[materia.Codigo] = new DemandaRowDto
            {
                MateriaCodigo = materia.Codigo,
                MateriaNombre = materia.Nombre,
                Facultad = materia.Facultad,
                Solicitudes = solicitudes,
                NumClases = clasesMateria.Count,
                Asientos = asientos,
                Brecha = brecha
            };
        }

        return demandas.Values
            .OrderByDescending(d => d.Brecha)
            .ThenBy(d => d.MateriaNombre)
            .ToList();
    }

    /// <summary>
    /// Actualiza demanda snapshot recalculando desde inscripciones
    /// </summary>
    public async Task<List<DemandaRowDto>> ActualizarDemandaAsync()
    {
        var clases = await GetClasesPeriodoActualAsync();
        var materias = await _repository.GetMateriasAsync();

        // Agrupar inscriptos por materia (si existiera tabla inscripcion)
        // Por ahora, usamos inscriptos de clase como proxy
        var solicitudesPorMateria = new Dictionary<string, int>();

        foreach (var materia in materias)
        {
            var clasesMateria = clases.Where(c => c.MateriaCodigo == materia.Codigo).ToList();
            var totalInscriptos = clasesMateria.Sum(c => c.Inscriptos);
            solicitudesPorMateria[materia.Codigo] = totalInscriptos;

            // Upsert en demanda_snapshot
            await _repository.UpsertDemandaSnapshotAsync(materia.Codigo, totalInscriptos);
        }

        // Retornar demanda actualizada
        return await GetDemandaAsync();
    }
}
