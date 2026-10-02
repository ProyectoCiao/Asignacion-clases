using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;

namespace UadeAulas.Api.Services;

public class CatalogService
{
    private readonly Repository _repository;

    public CatalogService(Repository repository)
    {
        _repository = repository;
    }

    public async Task<List<AulaDto>> GetAulasAsync(int? sedeId = null)
    {
        var aulas = await _repository.GetAulasAsync(sedeId);
        var sedes = await _repository.GetSedesAsync();

        return aulas
            .Select(a => new AulaDto
            {
                Id = a.Id,
                SedeId = a.SedeId,
                Codigo = a.Codigo,
                TipoGrupo = a.TipoGrupo,
                TipoDetalle = a.TipoDetalle,
                Piso = a.Piso,
                CapacidadReal = a.CapacidadReal,
                CapacidadTotal = a.CapacidadTotal,
                Equipamiento = a.Equipamiento,
                Activa = a.Activa,
                Edificio = a.Edificio,
                PisoTexto = a.PisoTexto,
                NumeroAula = a.NumeroAula,
                Sede = new SedeDto
                {
                    Id = a.SedeId,
                    Codigo = sedes.First(s => s.Id == a.SedeId).Codigo,
                    Nombre = sedes.First(s => s.Id == a.SedeId).Nombre
                }
            })
            .ToList();
    }

    public async Task<List<MateriaDto>> GetMateriasAsync()
    {
        var materias = await _repository.GetMateriasAsync();

        return materias
            .Select(m => new MateriaDto
            {
                Codigo = m.Codigo,
                Nombre = m.Nombre,
                Facultad = m.Facultad,
                Departamento = m.Departamento,
                CargaHoraria = m.CargaHoraria
            })
            .ToList();
    }

    public async Task<List<DocenteDto>> GetDocentesAsync()
    {
        var docentes = await _repository.GetDocentesAsync();

        return docentes
            .Select(d => new DocenteDto
            {
                Id = d.Id,
                Nombre = d.Nombre
            })
            .ToList();
    }
}
