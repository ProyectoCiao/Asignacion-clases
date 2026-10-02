using UadeAulas.Api.Data;
using UadeAulas.Api.Dtos;
using UadeAulas.Api.Models;

namespace UadeAulas.Api.Services;

public class SugerirService
{
    private readonly Repository _repository;

    public SugerirService(Repository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Sugiere aula óptima para una clase basado en reglas HARD y SOFT
    /// </summary>
    public async Task<SugerirResponse> SugerirAulaAsync(long claseId)
    {
        // Precargar datos
        var clase = await _repository.GetClaseByIdAsync(claseId);
        if (clase == null)
            throw new InvalidOperationException($"Clase {claseId} no encontrada");

        var asignacionActual = await _repository.GetAsignacionByClaseIdAsync(claseId);
        var aulas = await _repository.GetAulasAsync(clase.SedeId);
        var asignaciones = await _repository.GetAllAsignacionesAsync();
        var clasesPeriodo = (await _repository.GetClasesAsync(clase.SedeId, periodoId: clase.PeriodoId))
            .ToDictionary(c => c.Id);
        var materias = await _repository.GetMateriasAsync();
        var materia = materias.FirstOrDefault(m => m.Codigo == clase.MateriaCodigo);

        var response = new SugerirResponse
        {
            ClaseId = clase.Id,
            NroClase = clase.NroClase,
            Materia = materia?.Nombre ?? clase.MateriaCodigo,
            Inscriptos = clase.Inscriptos,
            Regimen = clase.Regimen ?? "PRESENCIAL",
            AulaActual = null
        };

        // Aula actual
        Aula? aulaActualObj = null;
        if (asignacionActual?.AulaId.HasValue == true)
        {
            aulaActualObj = aulas.FirstOrDefault(a => a.Id == asignacionActual.AulaId);
            if (aulaActualObj != null)
            {
                response.AulaActual = MapToAulaAltDto(aulaActualObj, clase.InscriptosConReserva);
            }
        }

        // REGLA HARD: Si es REMOTO, no sugerir aula física
        if (clase.Regimen?.ToUpper() == "REMOTO")
        {
            response.Recomendada = null;
            response.YaOptima = false;
            return response;
        }

        // Obtener aulas candidatas
        var aulasCandidatas = ObtenerAulasCandidatas(
            clase, aulas, asignaciones, clasesPeriodo, aulaActualObj);

        // Si no hay candidatas, devolver bloqueadas
        if (aulasCandidatas.Count == 0)
        {
            response.Bloqueadas = aulas
                .Where(a => a.Activa && a.TipoGrupo != "REMOTA" && a.TipoGrupo != "SIN_AULA")
                .Select(a => new AulaBloqueadaDto
                {
                    Codigo = a.Codigo,
                    Motivo = CalcularMotivoBloqueo(a, clase, asignaciones, clasesPeriodo)
                })
                .ToList();

            response.YaOptima = false;
            return response;
        }

        // Calcular puntajes
        var aulasConPuntaje = aulasCandidatas
            .Select(a => new
            {
                Aula = a,
                Puntaje = CalcularPuntaje(a, clase, aulaActualObj)
            })
            .OrderBy(x => x.Puntaje)
            .ToList();

        // Recomendada = menor puntaje
        var recomendada = aulasConPuntaje.First();
        response.Recomendada = MapToAulaAltDto(recomendada.Aula, clase.InscriptosConReserva);
        response.Recomendada.Puntaje = recomendada.Puntaje;

        // Alternativas = siguientes 2 mejores
        response.Alternativas = aulasConPuntaje
            .Skip(1)
            .Take(2)
            .Select(x => 
            {
                var dto = MapToAulaAltDto(x.Aula, clase.InscriptosConReserva);
                dto.Puntaje = x.Puntaje;
                return dto;
            })
            .ToList();

        // YaOptima = recomendada es el aula actual
        response.YaOptima = aulaActualObj != null && 
                           recomendada.Aula.Id == aulaActualObj.Id;

        return response;
    }

    /// <summary>
    /// REGLAS HARD: obtiene solo aulas que cumplen restricciones obligatorias
    /// </summary>
    private List<Aula> ObtenerAulasCandidatas(
        Clase clase,
        List<Aula> aulas,
        List<Asignacion> asignaciones,
        Dictionary<long, Clase> clasesPeriodo,
        Aula? aulaActual)
    {
        var candidatas = new List<Aula>();

        foreach (var aula in aulas)
        {
            // HARD 1: Solo aulas activas, NO REMOTA ni SIN_AULA, misma sede
            if (!aula.Activa || aula.TipoGrupo == "REMOTA" || aula.TipoGrupo == "SIN_AULA" ||
                aula.SedeId != clase.SedeId)
                continue;

            // HARD 2: capacidad_total >= inscriptos_con_reserva
            if (aula.CapacidadTotal < clase.InscriptosConReserva)
                continue;

            // HARD 3: No choque (otro clase en esa aula con intersección días + solapamiento horarios)
            if (HayChoque(clase, aula, asignaciones, clasesPeriodo) != null)
                continue;

            // HARD 4: Si aula actual es LAB/TALLER/ARTE, no bajar a tipo distinto
            if (aulaActual != null && 
                (aulaActual.TipoGrupo == "LAB" || aulaActual.TipoGrupo == "TALLER" || aulaActual.TipoGrupo == "ARTE"))
            {
                if (aula.TipoGrupo != aulaActual.TipoGrupo)
                    continue;
            }

            candidatas.Add(aula);
        }

        return candidatas;
    }

    /// <summary>
    /// Devuelve la clase del mismo período que ocupa el aula con algún día en común y horario
    /// solapado, o null si el aula está libre en ese horario.
    /// </summary>
    private Clase? HayChoque(Clase clase, Aula aula, List<Asignacion> asignaciones, Dictionary<long, Clase> clasesPeriodo)
    {
        var diasClase = clase.Dias ?? Array.Empty<string>();

        foreach (var asignacion in asignaciones.Where(a => a.AulaId == aula.Id && a.ClaseId != clase.Id))
        {
            if (!clasesPeriodo.TryGetValue(asignacion.ClaseId, out var otra))
                continue; // otro período u otra sede: no compite por el aula

            var compartenDia = (otra.Dias ?? Array.Empty<string>()).Intersect(diasClase).Any();
            var seSolapan = clase.HorarioDesde < otra.HorarioHasta && otra.HorarioDesde < clase.HorarioHasta;

            if (compartenDia && seSolapan)
                return otra;
        }

        return null;
    }

    /// <summary>
    /// SOFT: Calcula puntaje (menor = mejor)
    /// - sillas_vacias inicial
    /// - −4 si es el aula actual
    /// - −2 si mismo tipo_grupo que el actual
    /// - +18 si capacidad >= 70 e inscriptos <= 30
    /// </summary>
    private int CalcularPuntaje(Aula aula, Clase clase, Aula? aulaActual)
    {
        int puntaje = 0;

        // Puntaje inicial = sillas_vacias (capacidad_total - inscriptos)
        puntaje = aula.CapacidadTotal - clase.Inscriptos;

        // −4 si es el aula actual
        if (aulaActual != null && aula.Id == aulaActual.Id)
            puntaje -= 4;

        // −2 si mismo tipo_grupo que el actual
        if (aulaActual != null && aula.TipoGrupo == aulaActual.TipoGrupo)
            puntaje -= 2;

        // +18 si capacidad >= 70 e inscriptos <= 30
        if (aula.CapacidadTotal >= 70 && clase.Inscriptos <= 30)
            puntaje += 18;

        return puntaje;
    }

    /// <summary>
    /// Determina por qué un aula está bloqueada
    /// </summary>
    private string CalcularMotivoBloqueo(
        Aula aula, Clase clase, List<Asignacion> asignaciones, Dictionary<long, Clase> clasesPeriodo)
    {
        if (!aula.Activa)
            return "Aula inactiva";
        if (aula.TipoGrupo == "REMOTA")
            return "Aula remota";
        if (aula.TipoGrupo == "SIN_AULA")
            return "Sin aula física";
        if (aula.SedeId != clase.SedeId)
            return "Sede diferente";
        if (aula.CapacidadTotal < clase.InscriptosConReserva)
            return $"Capacidad insuficiente ({aula.CapacidadTotal} < {clase.InscriptosConReserva})";

        var choque = HayChoque(clase, aula, asignaciones, clasesPeriodo);
        if (choque != null)
            return $"Choque con clase {choque.NroClase} ({choque.HorarioDesde:HH:mm}-{choque.HorarioHasta:HH:mm})";

        return "No cumple requisitos";
    }

    private AulaAltDto MapToAulaAltDto(Aula aula, int inscriptosConReserva)
    {
        return new AulaAltDto
        {
            Id = aula.Id,
            Codigo = aula.Codigo,
            TipoGrupo = aula.TipoGrupo,
            CapacidadTotal = aula.CapacidadTotal,
            Piso = aula.Piso,
            SillasVacias = aula.CapacidadTotal - inscriptosConReserva,
            Puntaje = 0
        };
    }
}
