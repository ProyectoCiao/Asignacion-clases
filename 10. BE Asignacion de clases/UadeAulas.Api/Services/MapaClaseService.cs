using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;
using UadeAulas.Api.Models;

namespace UadeAulas.Api.Services;

public class MapaService
{
    private readonly Repository _repository;

    public MapaService(Repository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Obtiene mapa de aulas para un piso/sede/turno/día específico
    /// </summary>
    public async Task<List<MapaDto>> GetMapaAsync(int sedeId, int piso, string? turno = null, string? dia = null)
    {
        var aulas = await _repository.GetAulasAsync(sedeId);
        var periodo = await _repository.GetPeriodoActualAsync();
        var clases = periodo == null
            ? new List<Clase>()
            : await _repository.GetClasesAsync(sedeId, turno, periodo.Id);
        var asignaciones = await _repository.GetAllAsignacionesAsync();
        var materias = await _repository.GetMateriasAsync();
        var docentesPorClase = await _repository.GetDocentesPorClaseAsync();

        // Solo cuentan las clases del período vigente que pasan los filtros de turno y día
        var clasesFiltradas = clases
            .Where(c => string.IsNullOrEmpty(dia) || (c.Dias != null && c.Dias.Contains(dia)))
            .ToDictionary(c => c.Id);

        var aulasPiso = aulas.Where(a => a.Piso == piso && a.Activa).ToList();
        var mapa = new List<MapaDto>();

        foreach (var aula in aulasPiso)
        {
            // Clase que ocupa esta aula (si hay varias en el filtro, la de horario más temprano)
            var clase = asignaciones
                .Where(a => a.AulaId == aula.Id && clasesFiltradas.ContainsKey(a.ClaseId))
                .Select(a => clasesFiltradas[a.ClaseId])
                .OrderBy(c => c.HorarioDesde)
                .FirstOrDefault();

            if (clase == null)
            {
                mapa.Add(new MapaDto
                {
                    AulaId = aula.Id,
                    Codigo = aula.Codigo,
                    TipoGrupo = aula.TipoGrupo,
                    CapacidadTotal = aula.CapacidadTotal,
                    Ocupados = 0,
                    ClaseNro = null
                });
                continue;
            }

            var materia = materias.FirstOrDefault(m => m.Codigo == clase.MateriaCodigo);
            var docentes = docentesPorClase.GetValueOrDefault(clase.Id) ?? new List<Docente>();

            mapa.Add(new MapaDto
            {
                AulaId = aula.Id,
                Codigo = aula.Codigo,
                TipoGrupo = aula.TipoGrupo,
                CapacidadTotal = aula.CapacidadTotal,
                Ocupados = clase.InscriptosConReserva,
                ClaseNro = clase.NroClase,
                Materia = materia?.Nombre,
                Horario = $"{clase.HorarioDesde:HH:mm}-{clase.HorarioHasta:HH:mm}",
                Docentes = string.Join(", ", docentes.Select(d => d.Nombre))
            });
        }

        return mapa.OrderBy(m => m.Codigo).ToList();
    }
}

public class ClaseService
{
    private readonly Repository _repository;

    public ClaseService(Repository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Crea una nueva clase
    /// </summary>
    public async Task<CrearClaseResponse> CrearClaseAsync(CrearClaseRequest request)
    {
        // Validar materia
        var materias = await _repository.GetMateriasAsync();
        var materia = materias.FirstOrDefault(m => m.Codigo == request.MateriaCodigo);
        if (materia == null)
            throw new InvalidOperationException($"Materia {request.MateriaCodigo} no existe");

        // Obtener período vigente
        var periodo = await _repository.GetPeriodoActualAsync();
        if (periodo == null)
            throw new InvalidOperationException("No hay períodos cargados");

        // Obtener próximo nro_clase
        var nroClase = await _repository.GetNextNroClaseAsync();

        // Parsear horarios
        if (!TimeOnly.TryParse(request.HorarioDesde, out var horarioDesde))
            throw new InvalidOperationException("Formato de horario desde inválido");
        if (!TimeOnly.TryParse(request.HorarioHasta, out var horarioHasta))
            throw new InvalidOperationException("Formato de horario hasta inválido");

        // Crear clase
        var claseId = await _repository.CreateClaseAsync(
            nroClase, periodo.Id, request.SedeId, request.MateriaCodigo,
            request.Turno, horarioDesde, horarioHasta,
            request.Dias, request.Inscriptos);

        if (claseId == 0)
            throw new InvalidOperationException("Error al crear la clase");

        // Si hay aula, crear asignación
        if (request.AulaId.HasValue)
        {
            var aula = await _repository.GetAulaByIdAsync(request.AulaId.Value);
            if (aula != null)
            {
                var sillasVacias = aula.CapacidadTotal - request.Inscriptos;
                await _repository.UpsertAsignacionAsync(claseId, request.AulaId, sillasVacias, "MANUAL");
            }
        }

        return new CrearClaseResponse
        {
            ClaseId = claseId,
            NroClase = nroClase,
            Mensaje = $"Clase {nroClase} creada exitosamente"
        };
    }
}
