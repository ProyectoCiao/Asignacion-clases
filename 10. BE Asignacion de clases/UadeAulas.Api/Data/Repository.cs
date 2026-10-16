using Npgsql;
using UadeAulas.Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace UadeAulas.Api.Data;

public class Repository
{
    private readonly string _connectionString;

    public Repository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") 
            ?? throw new InvalidOperationException("Connection string not found");
    }

    private async Task<NpgsqlConnection> GetConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    // Sedes
    public async Task<List<Sede>> GetSedesAsync()
    {
        var sedes = new List<Sede>();
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand("SELECT id, codigo, nombre FROM sede ORDER BY nombre", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sedes.Add(new Sede
            {
                Id = reader.GetInt32(0),
                Codigo = reader.GetString(1),
                Nombre = reader.GetString(2)
            });
        }
        return sedes;
    }

    // Aulas
    public async Task<List<Aula>> GetAulasAsync(int? sedeId = null)
    {
        var aulas = new List<Aula>();
        using var conn = await GetConnectionAsync();

        var query = @"SELECT id, sede_id, codigo, tipo_grupo, tipo_detalle, piso, 
                           capacidad_real, capacidad_total, equipamiento, activa,
                           edificio, piso_texto, numero_aula 
                    FROM aula";

        if (sedeId.HasValue)
            query += " WHERE sede_id = @sedeId";

        query += " ORDER BY codigo";

        using var cmd = new NpgsqlCommand(query, conn);
        if (sedeId.HasValue)
            cmd.Parameters.AddWithValue("@sedeId", sedeId.Value);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            aulas.Add(new Aula
            {
                Id = reader.GetInt32(0),
                SedeId = reader.GetInt32(1),
                Codigo = reader.GetString(2),
                TipoGrupo = reader.GetString(3),
                TipoDetalle = reader.IsDBNull(4) ? null : reader.GetString(4),
                Piso = reader.GetInt32(5),
                CapacidadReal = reader.GetInt32(6),
                CapacidadTotal = reader.GetInt32(7),
                Equipamiento = reader.IsDBNull(8) ? null : (string[])reader.GetValue(8),
                Activa = reader.GetBoolean(9),
                Edificio = reader.IsDBNull(10) ? null : reader.GetString(10),
                PisoTexto = reader.IsDBNull(11) ? null : reader.GetString(11),
                NumeroAula = reader.IsDBNull(12) ? null : reader.GetString(12)
            });
        }
        return aulas;
    }

    // Materias
    public async Task<List<Materia>> GetMateriasAsync()
    {
        var materias = new List<Materia>();
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT codigo, nombre, facultad, departamento, carga_horaria 
                                             FROM materia ORDER BY nombre", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            materias.Add(new Materia
            {
                Codigo = reader.GetString(0),
                Nombre = reader.GetString(1),
                Facultad = reader.IsDBNull(2) ? null : reader.GetString(2),
                Departamento = reader.IsDBNull(3) ? null : reader.GetString(3),
                CargaHoraria = reader.GetInt32(4)
            });
        }
        return materias;
    }

    // Docentes
    public async Task<List<Docente>> GetDocentesAsync()
    {
        var docentes = new List<Docente>();
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand("SELECT id, nombre FROM docente ORDER BY nombre", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            docentes.Add(new Docente
            {
                Id = reader.GetInt32(0),
                Nombre = reader.GetString(1)
            });
        }
        return docentes;
    }

    // Periodos
    private const string PeriodoColumnas = @"p.id, p.ciclo, p.nombre, p.fecha_desde, p.fecha_hasta,
                                              p.fecha_congelamiento, p.fecha_fin_inscripcion,
                                              p.reabierta_en, p.reabierta_por, p.reabierta_motivo,
                                              p.origen, p.periodo_origen_id";

    private static Periodo LeerPeriodo(NpgsqlDataReader reader)
    {
        return new Periodo
        {
            Id = reader.GetInt32(0),
            Ciclo = reader.GetString(1),
            Nombre = reader.GetString(2),
            FechaDesde = reader.GetDateTime(3),
            FechaHasta = reader.GetDateTime(4),
            FechaCongelamiento = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            FechaFinInscripcion = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
            ReabiertaEn = reader.IsDBNull(7) ? null : reader.GetDateTime(7),
            ReabiertaPor = reader.IsDBNull(8) ? null : reader.GetString(8),
            ReabiertaMotivo = reader.IsDBNull(9) ? null : reader.GetString(9),
            Origen = reader.GetString(10),
            PeriodoOrigenId = reader.IsDBNull(11) ? null : reader.GetInt32(11)
        };
    }

    public async Task<List<Periodo>> GetPeriodosAsync()
    {
        var periodos = new List<Periodo>();
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand($"SELECT {PeriodoColumnas} FROM periodo p ORDER BY p.fecha_desde DESC", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            periodos.Add(LeerPeriodo(reader));
        return periodos;
    }

    public async Task<Periodo?> GetPeriodoByIdAsync(int periodoId)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand($"SELECT {PeriodoColumnas} FROM periodo p WHERE p.id = @id", conn);
        cmd.Parameters.AddWithValue("@id", periodoId);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? LeerPeriodo(reader) : null;
    }

    /// <summary>
    /// Período vigente = el más reciente (por fecha_desde) que tiene clases cargadas.
    /// Si ninguno tiene clases, devuelve el más reciente; null si no hay períodos.
    /// </summary>
    public async Task<Periodo?> GetPeriodoActualAsync()
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand($@"SELECT {PeriodoColumnas}
                                             FROM periodo p
                                             ORDER BY EXISTS (SELECT 1 FROM clase c WHERE c.periodo_id = p.id) DESC,
                                                      p.fecha_desde DESC
                                             LIMIT 1", conn);
        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? LeerPeriodo(reader) : null;
    }

    // Clases con joins
    public async Task<List<Clase>> GetClasesAsync(int? sedeId = null, string? turno = null, int? periodoId = null)
    {
        var clases = new List<Clase>();
        using var conn = await GetConnectionAsync();

        var query = @"SELECT c.id, c.nro_clase, c.periodo_id, c.sede_id, c.materia_codigo,
                           c.turno, c.horario_desde, c.horario_hasta, c.dias, c.regimen,
                           c.idioma, c.modalidad, c.inscriptos, c.inscriptos_con_reserva, c.packs
                    FROM clase c WHERE 1=1";

        if (periodoId.HasValue)
            query += " AND c.periodo_id = @periodoId";
        if (sedeId.HasValue)
            query += " AND c.sede_id = @sedeId";
        if (!string.IsNullOrEmpty(turno))
            query += " AND c.turno = @turno";

        query += " ORDER BY c.nro_clase";

        using var cmd = new NpgsqlCommand(query, conn);
        if (periodoId.HasValue)
            cmd.Parameters.AddWithValue("@periodoId", periodoId.Value);
        if (sedeId.HasValue)
            cmd.Parameters.AddWithValue("@sedeId", sedeId.Value);
        if (!string.IsNullOrEmpty(turno))
            cmd.Parameters.AddWithValue("@turno", turno);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            clases.Add(new Clase
            {
                Id = reader.GetInt64(0),
                NroClase = reader.GetInt32(1),
                PeriodoId = reader.GetInt32(2),
                SedeId = reader.GetInt32(3),
                MateriaCodigo = reader.GetString(4),
                Turno = reader.GetString(5),
                HorarioDesde = reader.GetFieldValue<TimeOnly>(6),
                HorarioHasta = reader.GetFieldValue<TimeOnly>(7),
                Dias = reader.IsDBNull(8) ? null : (string[])reader.GetValue(8),
                Regimen = reader.IsDBNull(9) ? null : reader.GetString(9),
                Idioma = reader.IsDBNull(10) ? null : reader.GetString(10),
                Modalidad = reader.IsDBNull(11) ? null : reader.GetString(11),
                Inscriptos = reader.GetInt32(12),
                InscriptosConReserva = reader.GetInt32(13),
                Packs = reader.IsDBNull(14) ? null : (string[])reader.GetValue(14)
            });
        }
        return clases;
    }

    // Asignaciones
    public async Task<Asignacion?> GetAsignacionByClaseIdAsync(long claseId)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT id, clase_id, aula_id, origen, sillas_vacias 
                                             FROM asignacion WHERE clase_id = @claseId", conn);
        cmd.Parameters.AddWithValue("@claseId", claseId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Asignacion
            {
                Id = reader.GetInt64(0),
                ClaseId = reader.GetInt64(1),
                AulaId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Origen = reader.GetString(3),
                SillasVacias = reader.GetInt32(4)
            };
        }
        return null;
    }

    // Docentes de una clase
    public async Task<List<Docente>> GetDocentesByClaseIdAsync(long claseId)
    {
        var docentes = new List<Docente>();
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT d.id, d.nombre 
                                             FROM docente d
                                             INNER JOIN clase_docente cd ON d.id = cd.docente_id
                                             WHERE cd.clase_id = @claseId
                                             ORDER BY d.nombre", conn);
        cmd.Parameters.AddWithValue("@claseId", claseId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            docentes.Add(new Docente
            {
                Id = reader.GetInt32(0),
                Nombre = reader.GetString(1)
            });
        }
        return docentes;
    }

    // Docentes de todas las clases, agrupados por clase (evita una consulta por clase)
    public async Task<Dictionary<long, List<Docente>>> GetDocentesPorClaseAsync()
    {
        var resultado = new Dictionary<long, List<Docente>>();
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT cd.clase_id, d.id, d.nombre
                                             FROM docente d
                                             INNER JOIN clase_docente cd ON d.id = cd.docente_id
                                             ORDER BY d.nombre", conn);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var claseId = reader.GetInt64(0);
            if (!resultado.TryGetValue(claseId, out var docentes))
                resultado[claseId] = docentes = new List<Docente>();

            docentes.Add(new Docente
            {
                Id = reader.GetInt32(1),
                Nombre = reader.GetString(2)
            });
        }
        return resultado;
    }

    // Actualizar clase (turno e inscriptos_con_reserva)
    public async Task UpdateClaseAsync(long claseId, string turno, int inscriptosConReserva)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"UPDATE clase 
                                             SET turno = @turno, inscriptos_con_reserva = @inscriptosConReserva
                                             WHERE id = @claseId", conn);
        cmd.Parameters.AddWithValue("@claseId", claseId);
        cmd.Parameters.AddWithValue("@turno", turno);
        cmd.Parameters.AddWithValue("@inscriptosConReserva", inscriptosConReserva);

        await cmd.ExecuteNonQueryAsync();
    }

    // Actualizar o crear asignación
    public async Task UpsertAsignacionAsync(long claseId, int? aulaId, int sillasVacias, string origen)
    {
        using var conn = await GetConnectionAsync();

        // Primero intentar actualizar
        using var cmdUpdate = new NpgsqlCommand(@"UPDATE asignacion 
                                                   SET aula_id = @aulaId, sillas_vacias = @sillasVacias, origen = @origen
                                                   WHERE clase_id = @claseId", conn);
        cmdUpdate.Parameters.AddWithValue("@claseId", claseId);
        cmdUpdate.Parameters.AddWithValue("@aulaId", aulaId ?? (object)DBNull.Value);
        cmdUpdate.Parameters.AddWithValue("@sillasVacias", sillasVacias);
        cmdUpdate.Parameters.AddWithValue("@origen", origen);

        var rowsAffected = await cmdUpdate.ExecuteNonQueryAsync();

        // Si no actualizó nada, insertar
        if (rowsAffected == 0)
        {
            using var cmdInsert = new NpgsqlCommand(@"INSERT INTO asignacion (clase_id, aula_id, sillas_vacias, origen)
                                                      VALUES (@claseId, @aulaId, @sillasVacias, @origen)", conn);
            cmdInsert.Parameters.AddWithValue("@claseId", claseId);
            cmdInsert.Parameters.AddWithValue("@aulaId", aulaId ?? (object)DBNull.Value);
            cmdInsert.Parameters.AddWithValue("@sillasVacias", sillasVacias);
            cmdInsert.Parameters.AddWithValue("@origen", origen);

            await cmdInsert.ExecuteNonQueryAsync();
        }
    }

    // Obtener todas las asignaciones (para detectar choques)
    public async Task<List<Asignacion>> GetAllAsignacionesAsync()
    {
        var asignaciones = new List<Asignacion>();
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT id, clase_id, aula_id, origen, sillas_vacias 
                                             FROM asignacion", conn);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            asignaciones.Add(new Asignacion
            {
                Id = reader.GetInt64(0),
                ClaseId = reader.GetInt64(1),
                AulaId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Origen = reader.GetString(3),
                SillasVacias = reader.GetInt32(4)
            });
        }
        return asignaciones;
    }

    // Obtener aula por ID
    public async Task<Aula?> GetAulaByIdAsync(int aulaId)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT id, sede_id, codigo, tipo_grupo, tipo_detalle, piso, 
                                                   capacidad_real, capacidad_total, equipamiento, activa,
                                                   edificio, piso_texto, numero_aula
                                            FROM aula WHERE id = @aulaId", conn);
        cmd.Parameters.AddWithValue("@aulaId", aulaId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Aula
            {
                Id = reader.GetInt32(0),
                SedeId = reader.GetInt32(1),
                Codigo = reader.GetString(2),
                TipoGrupo = reader.GetString(3),
                TipoDetalle = reader.IsDBNull(4) ? null : reader.GetString(4),
                Piso = reader.GetInt32(5),
                CapacidadReal = reader.GetInt32(6),
                CapacidadTotal = reader.GetInt32(7),
                Equipamiento = reader.IsDBNull(8) ? null : (string[])reader.GetValue(8),
                Activa = reader.GetBoolean(9),
                Edificio = reader.IsDBNull(10) ? null : reader.GetString(10),
                PisoTexto = reader.IsDBNull(11) ? null : reader.GetString(11),
                NumeroAula = reader.IsDBNull(12) ? null : reader.GetString(12)
            };
        }
        return null;
    }

    // Obtener clase por ID
    public async Task<Clase?> GetClaseByIdAsync(long claseId)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT id, nro_clase, periodo_id, sede_id, materia_codigo, 
                                                   turno, horario_desde, horario_hasta, dias, regimen, 
                                                   idioma, modalidad, inscriptos, inscriptos_con_reserva, packs
                                            FROM clase WHERE id = @claseId", conn);
        cmd.Parameters.AddWithValue("@claseId", claseId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new Clase
            {
                Id = reader.GetInt64(0),
                NroClase = reader.GetInt32(1),
                PeriodoId = reader.GetInt32(2),
                SedeId = reader.GetInt32(3),
                MateriaCodigo = reader.GetString(4),
                Turno = reader.GetString(5),
                HorarioDesde = reader.GetFieldValue<TimeOnly>(6),
                HorarioHasta = reader.GetFieldValue<TimeOnly>(7),
                Dias = reader.IsDBNull(8) ? null : (string[])reader.GetValue(8),
                Regimen = reader.IsDBNull(9) ? null : reader.GetString(9),
                Idioma = reader.IsDBNull(10) ? null : reader.GetString(10),
                Modalidad = reader.IsDBNull(11) ? null : reader.GetString(11),
                Inscriptos = reader.GetInt32(12),
                InscriptosConReserva = reader.GetInt32(13),
                Packs = reader.IsDBNull(14) ? null : (string[])reader.GetValue(14)
            };
        }
        return null;
    }

    // Obtener demanda snapshot por materia
    public async Task<Dictionary<string, int>> GetDemandaSnapshotAsync()
    {
        var demanda = new Dictionary<string, int>();
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT materia_codigo, solicitudes 
                                             FROM demanda_snapshot", conn);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            demanda[reader.GetString(0)] = reader.GetInt32(1);
        }
        return demanda;
    }

    // Upsert demanda snapshot
    public async Task UpsertDemandaSnapshotAsync(string materiaCodigo, int solicitudes)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"INSERT INTO demanda_snapshot (materia_codigo, solicitudes)
                                             VALUES (@codigo, @solicitudes)
                                             ON CONFLICT (materia_codigo) DO UPDATE SET solicitudes = @solicitudes", conn);
        cmd.Parameters.AddWithValue("@codigo", materiaCodigo);
        cmd.Parameters.AddWithValue("@solicitudes", solicitudes);

        await cmd.ExecuteNonQueryAsync();
    }

    // Obtener próximo nro_clase
    public async Task<int> GetNextNroClaseAsync()
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"SELECT COALESCE(MAX(nro_clase), 0) + 1 FROM clase", conn);

        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 1;
    }

    // Crear nueva clase
    public async Task<long> CreateClaseAsync(
        int nroClase, int periodoId, int sedeId, string materiaCodigo,
        string turno, TimeOnly horarioDesde, TimeOnly horarioHasta,
        string[] dias, int inscriptos)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"INSERT INTO clase 
                                             (nro_clase, periodo_id, sede_id, materia_codigo, turno, 
                                              horario_desde, horario_hasta, dias, inscriptos, inscriptos_con_reserva)
                                             VALUES (@nro, @periodo, @sede, @materia, @turno, 
                                                     @desde, @hasta, @dias, @inscriptos, @inscriptos)
                                             RETURNING id", conn);
        cmd.Parameters.AddWithValue("@nro", nroClase);
        cmd.Parameters.AddWithValue("@periodo", periodoId);
        cmd.Parameters.AddWithValue("@sede", sedeId);
        cmd.Parameters.AddWithValue("@materia", materiaCodigo);
        cmd.Parameters.AddWithValue("@turno", turno);
        cmd.Parameters.AddWithValue("@desde", horarioDesde);
        cmd.Parameters.AddWithValue("@hasta", horarioHasta);
        cmd.Parameters.AddWithValue("@dias", dias);
        cmd.Parameters.AddWithValue("@inscriptos", inscriptos);

        var result = await cmd.ExecuteScalarAsync();
        return result != null ? (long)result : 0;
    }

    // Período por ciclo (ej. "2026-1")
    public async Task<Periodo?> GetPeriodoByCicloAsync(string ciclo)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand($"SELECT {PeriodoColumnas} FROM periodo p WHERE p.ciclo = @ciclo", conn);
        cmd.Parameters.AddWithValue("@ciclo", ciclo);

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync() ? LeerPeriodo(reader) : null;
    }

    public async Task<int> CreatePeriodoAsync(string ciclo, string nombre, DateTime fechaDesde, DateTime fechaHasta)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand(@"INSERT INTO periodo (ciclo, nombre, fecha_desde, fecha_hasta)
                                             VALUES (@ciclo, @nombre, @desde, @hasta)
                                             RETURNING id", conn);
        cmd.Parameters.AddWithValue("@ciclo", ciclo);
        cmd.Parameters.AddWithValue("@nombre", nombre);
        cmd.Parameters.AddWithValue("@desde", fechaDesde);
        cmd.Parameters.AddWithValue("@hasta", fechaHasta);

        var result = await cmd.ExecuteScalarAsync();
        return (int)result!;
    }

    public async Task<int> CreateDocenteAsync(string nombre)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand("INSERT INTO docente (nombre) VALUES (@nombre) RETURNING id", conn);
        cmd.Parameters.AddWithValue("@nombre", nombre);

        var result = await cmd.ExecuteScalarAsync();
        return (int)result!;
    }

    public async Task<bool> PeriodoTieneClasesAsync(int periodoId)
    {
        using var conn = await GetConnectionAsync();
        using var cmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM clase WHERE periodo_id = @periodoId)", conn);
        cmd.Parameters.AddWithValue("@periodoId", periodoId);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>
    /// Carga inicial de la planificación de un período desde el Excel (clase/clase_docente/asignacion) en una
    /// única transacción, junto con su registro de auditoría. Nunca pisa una planificación existente: si el
    /// período ya tiene clases no inserta nada y devuelve false. Si algo falla, no queda nada aplicado.
    /// </summary>
    public async Task<bool> InsertPlanificacionInicialAsync(int periodoId, List<PlanificacionFilaInsert> filas, Auditoria auditoria)
    {
        using var conn = await GetConnectionAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            // Bloquea la fila del período para que dos cargas simultáneas no pasen ambas el chequeo
            using (var cmdLock = new NpgsqlCommand("SELECT id FROM periodo WHERE id = @periodoId FOR UPDATE", conn, tx))
            {
                cmdLock.Parameters.AddWithValue("@periodoId", periodoId);
                await cmdLock.ExecuteNonQueryAsync();
            }

            using (var cmdExiste = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM clase WHERE periodo_id = @periodoId)", conn, tx))
            {
                cmdExiste.Parameters.AddWithValue("@periodoId", periodoId);
                if ((bool)(await cmdExiste.ExecuteScalarAsync())!)
                {
                    await tx.RollbackAsync();
                    return false;
                }
            }

            int proximoNro;
            using (var cmdMax = new NpgsqlCommand("SELECT COALESCE(MAX(nro_clase), 0) + 1 FROM clase", conn, tx))
            {
                proximoNro = Convert.ToInt32(await cmdMax.ExecuteScalarAsync());
            }

            foreach (var fila in filas)
            {
                long claseId;
                using (var cmdClase = new NpgsqlCommand(@"INSERT INTO clase 
                            (nro_clase, periodo_id, sede_id, materia_codigo, turno, horario_desde, horario_hasta, 
                             dias, regimen, idioma, inscriptos, inscriptos_con_reserva)
                            VALUES (@nro, @periodo, @sede, @materia, @turno, @desde, @hasta, @dias, @regimen, @idioma, @inscriptos, @inscriptos)
                            RETURNING id", conn, tx))
                {
                    cmdClase.Parameters.AddWithValue("@nro", proximoNro);
                    cmdClase.Parameters.AddWithValue("@periodo", periodoId);
                    cmdClase.Parameters.AddWithValue("@sede", fila.SedeId);
                    cmdClase.Parameters.AddWithValue("@materia", fila.MateriaCodigo);
                    cmdClase.Parameters.AddWithValue("@turno", fila.Turno);
                    cmdClase.Parameters.AddWithValue("@desde", fila.HorarioDesde);
                    cmdClase.Parameters.AddWithValue("@hasta", fila.HorarioHasta);
                    cmdClase.Parameters.AddWithValue("@dias", fila.Dias);
                    cmdClase.Parameters.AddWithValue("@regimen", (object?)fila.Regimen ?? DBNull.Value);
                    cmdClase.Parameters.AddWithValue("@idioma", (object?)fila.Idioma ?? DBNull.Value);
                    cmdClase.Parameters.AddWithValue("@inscriptos", fila.Inscriptos);

                    claseId = (long)(await cmdClase.ExecuteScalarAsync())!;
                }

                if (fila.AulaId.HasValue)
                {
                    var sillasVacias = (fila.AulaCapacidadTotal ?? fila.Inscriptos) - fila.Inscriptos;

                    using var cmdAsig = new NpgsqlCommand(@"INSERT INTO asignacion (clase_id, aula_id, sillas_vacias, origen)
                                                             VALUES (@claseId, @aulaId, @sillas, 'EXCEL')", conn, tx);
                    cmdAsig.Parameters.AddWithValue("@claseId", claseId);
                    cmdAsig.Parameters.AddWithValue("@aulaId", fila.AulaId.Value);
                    cmdAsig.Parameters.AddWithValue("@sillas", sillasVacias);
                    await cmdAsig.ExecuteNonQueryAsync();
                }

                foreach (var docenteId in fila.DocenteIds)
                {
                    using var cmdCd = new NpgsqlCommand(@"INSERT INTO clase_docente (clase_id, docente_id) 
                                                           VALUES (@claseId, @docenteId)
                                                           ON CONFLICT DO NOTHING", conn, tx);
                    cmdCd.Parameters.AddWithValue("@claseId", claseId);
                    cmdCd.Parameters.AddWithValue("@docenteId", docenteId);
                    await cmdCd.ExecuteNonQueryAsync();
                }

                proximoNro++;
            }

            await InsertAuditoriaAsync(conn, tx, auditoria);
            await tx.CommitAsync();
            return true;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Actualiza las fechas de congelamiento y fin de inscripción de un período y registra el cambio en la
    /// misma transacción.
    /// </summary>
    public async Task UpdateFechasPlanificacionAsync(int periodoId, DateTime? fechaCongelamiento, DateTime? fechaFinInscripcion, Auditoria auditoria)
    {
        using var conn = await GetConnectionAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            using (var cmd = new NpgsqlCommand(@"UPDATE periodo
                                                  SET fecha_congelamiento = @congelamiento, fecha_fin_inscripcion = @finInscripcion
                                                  WHERE id = @periodoId", conn, tx))
            {
                cmd.Parameters.Add(new NpgsqlParameter("@congelamiento", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)fechaCongelamiento ?? DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter("@finInscripcion", NpgsqlTypes.NpgsqlDbType.TimestampTz) { Value = (object?)fechaFinInscripcion ?? DBNull.Value });
                cmd.Parameters.AddWithValue("@periodoId", periodoId);
                await cmd.ExecuteNonQueryAsync();
            }

            await InsertAuditoriaAsync(conn, tx, auditoria);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Aplica un lote de cambios de clases (ya validado sobre el estado final) en una única transacción, con
    /// su auditoría. Bloquea la fila del período para serializar ediciones simultáneas del mismo período.
    /// </summary>
    public async Task AplicarCambiosClasesAsync(int periodoId, List<ClaseEdicion> clases, List<Auditoria> auditorias)
    {
        using var conn = await GetConnectionAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            using (var cmdLock = new NpgsqlCommand("SELECT id FROM periodo WHERE id = @periodoId FOR UPDATE", conn, tx))
            {
                cmdLock.Parameters.AddWithValue("@periodoId", periodoId);
                await cmdLock.ExecuteNonQueryAsync();
            }

            foreach (var c in clases)
            {
                using (var cmd = new NpgsqlCommand(@"UPDATE clase
                            SET materia_codigo = @materia, turno = @turno, dias = @dias,
                                horario_desde = @desde, horario_hasta = @hasta
                            WHERE id = @claseId AND periodo_id = @periodoId", conn, tx))
                {
                    cmd.Parameters.AddWithValue("@materia", c.MateriaCodigo);
                    cmd.Parameters.AddWithValue("@turno", c.Turno);
                    cmd.Parameters.AddWithValue("@dias", c.Dias);
                    cmd.Parameters.AddWithValue("@desde", c.HorarioDesde);
                    cmd.Parameters.AddWithValue("@hasta", c.HorarioHasta);
                    cmd.Parameters.AddWithValue("@claseId", c.ClaseId);
                    cmd.Parameters.AddWithValue("@periodoId", periodoId);
                    if (await cmd.ExecuteNonQueryAsync() != 1)
                        throw new InvalidOperationException($"La clase {c.ClaseId} no pertenece al período {periodoId}.");
                }

                if (c.DocenteIds != null)
                {
                    using (var cmdDel = new NpgsqlCommand("DELETE FROM clase_docente WHERE clase_id = @claseId", conn, tx))
                    {
                        cmdDel.Parameters.AddWithValue("@claseId", c.ClaseId);
                        await cmdDel.ExecuteNonQueryAsync();
                    }
                    foreach (var docenteId in c.DocenteIds)
                    {
                        using var cmdIns = new NpgsqlCommand(@"INSERT INTO clase_docente (clase_id, docente_id)
                                                                VALUES (@claseId, @docenteId) ON CONFLICT DO NOTHING", conn, tx);
                        cmdIns.Parameters.AddWithValue("@claseId", c.ClaseId);
                        cmdIns.Parameters.AddWithValue("@docenteId", docenteId);
                        await cmdIns.ExecuteNonQueryAsync();
                    }
                }

                if (c.AulaId.HasValue)
                {
                    using var cmdAsig = new NpgsqlCommand(@"INSERT INTO asignacion (clase_id, aula_id, sillas_vacias, origen)
                                VALUES (@claseId, @aulaId, @sillas, 'MANUAL')
                                ON CONFLICT (clase_id) DO UPDATE
                                SET aula_id = EXCLUDED.aula_id, sillas_vacias = EXCLUDED.sillas_vacias, origen = 'MANUAL'", conn, tx);
                    cmdAsig.Parameters.AddWithValue("@claseId", c.ClaseId);
                    cmdAsig.Parameters.AddWithValue("@aulaId", c.AulaId.Value);
                    cmdAsig.Parameters.AddWithValue("@sillas", c.SillasVacias);
                    await cmdAsig.ExecuteNonQueryAsync();
                }
            }

            foreach (var a in auditorias)
                await InsertAuditoriaAsync(conn, tx, a);

            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Copia la planificación de un período (clase, clase_docente, asignacion) a otro período, que se crea si no
    /// existe, en una única transacción y con su auditoría. Las clases nuevas arrancan sin inscriptos; los
    /// docentes y las aulas se copian tal cual. Devuelve null, sin aplicar nada, si el destino ya tiene clases.
    /// </summary>
    public async Task<CopiaPlanificacionResultado?> CopiarPlanificacionAsync(
        int periodoOrigenId, string ciclo, string nombre, DateTime fechaDesde, DateTime fechaHasta,
        Func<CopiaPlanificacionResultado, Auditoria> crearAuditoria)
    {
        using var conn = await GetConnectionAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            async Task<object?> Escalar(string sql, params (string, object)[] ps)
            {
                using var cmd = new NpgsqlCommand(sql, conn, tx);
                foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
                return await cmd.ExecuteScalarAsync();
            }
            async Task<int> Ejecutar(string sql, params (string, object)[] ps)
            {
                using var cmd = new NpgsqlCommand(sql, conn, tx);
                foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v);
                return await cmd.ExecuteNonQueryAsync();
            }

            // Período destino: el existente (bloqueado) o uno nuevo
            var destino = await Escalar("SELECT id FROM periodo WHERE ciclo = @ciclo FOR UPDATE", ("@ciclo", ciclo));
            int periodoId;
            if (destino != null)
            {
                periodoId = (int)destino;
                if ((bool)(await Escalar("SELECT EXISTS (SELECT 1 FROM clase WHERE periodo_id = @p)", ("@p", periodoId)))!)
                {
                    await tx.RollbackAsync();
                    return null;
                }
                await Ejecutar("UPDATE periodo SET origen = 'COPIA', periodo_origen_id = @origen WHERE id = @p",
                    ("@origen", periodoOrigenId), ("@p", periodoId));
            }
            else
            {
                periodoId = (int)(await Escalar(@"INSERT INTO periodo (ciclo, nombre, fecha_desde, fecha_hasta, origen, periodo_origen_id)
                                                  VALUES (@ciclo, @nombre, @desde, @hasta, 'COPIA', @origen) RETURNING id",
                    ("@ciclo", ciclo), ("@nombre", nombre), ("@desde", fechaDesde), ("@hasta", fechaHasta), ("@origen", periodoOrigenId)))!;
            }

            // Mapeo clase origen -> clase nueva (id de la secuencia y nro_clase correlativo al máximo actual)
            await Ejecutar(@"CREATE TEMP TABLE map_clase ON COMMIT DROP AS
                             SELECT c.id AS old_id,
                                    nextval(pg_get_serial_sequence('clase', 'id')) AS new_id,
                                    (SELECT COALESCE(MAX(nro_clase), 0) FROM clase)
                                        + row_number() OVER (ORDER BY c.nro_clase, c.id) AS nro
                             FROM clase c WHERE c.periodo_id = @origen", ("@origen", periodoOrigenId));

            var resultado = new CopiaPlanificacionResultado { PeriodoId = periodoId };

            resultado.Clases = await Ejecutar(@"INSERT INTO clase (id, nro_clase, periodo_id, sede_id, materia_codigo, turno,
                                                    horario_desde, horario_hasta, dias, regimen, idioma, modalidad,
                                                    inscriptos, inscriptos_con_reserva, packs)
                             SELECT m.new_id, m.nro, @destino, c.sede_id, c.materia_codigo, c.turno,
                                    c.horario_desde, c.horario_hasta, c.dias, c.regimen, c.idioma, c.modalidad,
                                    0, 0, c.packs
                             FROM clase c JOIN map_clase m ON m.old_id = c.id", ("@destino", periodoId));

            resultado.Docentes = await Ejecutar(@"INSERT INTO clase_docente (clase_id, docente_id)
                             SELECT m.new_id, cd.docente_id FROM clase_docente cd JOIN map_clase m ON m.old_id = cd.clase_id");

            // Sin inscriptos todavía: sillas vacías = capacidad del aula
            resultado.Asignaciones = await Ejecutar(@"INSERT INTO asignacion (clase_id, aula_id, origen, sillas_vacias)
                             SELECT m.new_id, a.aula_id, 'COPIA', COALESCE(au.capacidad_total, 0)
                             FROM asignacion a JOIN map_clase m ON m.old_id = a.clase_id
                             LEFT JOIN aula au ON au.id = a.aula_id");

            // Clases que quedan en aulas hoy inactivas: se informan para que el backoffice las mueva
            using (var cmd = new NpgsqlCommand(@"SELECT c.nro_clase, au.codigo
                             FROM clase c JOIN asignacion a ON a.clase_id = c.id JOIN aula au ON au.id = a.aula_id
                             WHERE c.periodo_id = @destino AND NOT au.activa ORDER BY c.nro_clase", conn, tx))
            {
                cmd.Parameters.AddWithValue("@destino", periodoId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    resultado.ClasesEnAulasInactivas.Add($"Clase {reader.GetInt32(0)}: el aula {reader.GetString(1)} está inactiva.");
            }

            await InsertAuditoriaAsync(conn, tx, crearAuditoria(resultado));
            await tx.CommitAsync();
            return resultado;
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Abre (usuario y motivo) o cierra (nulls) la reapertura vigente de un período, con su auditoría, en una transacción.
    /// </summary>
    public async Task SetReaperturaAsync(int periodoId, bool reabrir, string? usuario, string? motivo, Auditoria auditoria)
    {
        using var conn = await GetConnectionAsync();
        using var tx = await conn.BeginTransactionAsync();
        try
        {
            using (var cmd = new NpgsqlCommand(@"UPDATE periodo
                        SET reabierta_en = CASE WHEN @reabrir THEN now() END,
                            reabierta_por = @usuario, reabierta_motivo = @motivo
                        WHERE id = @periodoId", conn, tx))
            {
                cmd.Parameters.AddWithValue("@reabrir", reabrir);
                cmd.Parameters.Add(new NpgsqlParameter("@usuario", NpgsqlTypes.NpgsqlDbType.Varchar) { Value = reabrir ? usuario! : DBNull.Value });
                cmd.Parameters.Add(new NpgsqlParameter("@motivo", NpgsqlTypes.NpgsqlDbType.Text) { Value = reabrir ? motivo! : DBNull.Value });
                cmd.Parameters.AddWithValue("@periodoId", periodoId);
                await cmd.ExecuteNonQueryAsync();
            }

            await InsertAuditoriaAsync(conn, tx, auditoria);
            await tx.CommitAsync();
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    // Auditoría: siempre dentro de la transacción del cambio que registra
    private static async Task InsertAuditoriaAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Auditoria a)
    {
        using var cmd = new NpgsqlCommand(@"INSERT INTO auditoria_cambios
                    (usuario, periodo_id, entidad, entidad_id, accion, es_emergencia, motivo, antes, despues, lote)
                    VALUES (@usuario, @periodoId, @entidad, @entidadId, @accion, @emergencia, @motivo, @antes, @despues, @lote)", conn, tx);
        cmd.Parameters.Add(new NpgsqlParameter("@lote", NpgsqlTypes.NpgsqlDbType.Uuid) { Value = (object?)a.Lote ?? DBNull.Value });
        cmd.Parameters.AddWithValue("@usuario", a.Usuario);
        cmd.Parameters.AddWithValue("@periodoId", (object?)a.PeriodoId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@entidad", a.Entidad);
        cmd.Parameters.AddWithValue("@entidadId", (object?)a.EntidadId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@accion", a.Accion);
        cmd.Parameters.AddWithValue("@emergencia", a.EsEmergencia);
        cmd.Parameters.AddWithValue("@motivo", (object?)a.Motivo ?? DBNull.Value);
        cmd.Parameters.Add(new NpgsqlParameter("@antes", NpgsqlTypes.NpgsqlDbType.Jsonb) { Value = ToJson(a.Antes) });
        cmd.Parameters.Add(new NpgsqlParameter("@despues", NpgsqlTypes.NpgsqlDbType.Jsonb) { Value = ToJson(a.Despues) });
        await cmd.ExecuteNonQueryAsync();
    }

    private static object ToJson(object? valor) =>
        valor == null ? DBNull.Value : System.Text.Json.JsonSerializer.Serialize(valor);
}
