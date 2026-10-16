using Npgsql;
using UadeAulas.Api.Dtos;

namespace UadeAulas.Api.Data;

/// <summary>Tablas maestras que alimentan los desplegables del front (sedes, turno, edificios, materias, aulas).</summary>
public class TablasMaestrasRepository
{
    private readonly string _connectionString;

    public TablasMaestrasRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found");
    }

    // Se ordena por id: el orden de carga de cada tabla es el que se muestra (ej. Mañana, Tarde, Noche)
    public Task<List<ItemMaestroDto>> GetSedesAsync() =>
        ListarAsync("SELECT id, nombre_sede FROM sedes WHERE nombre_sede IS NOT NULL ORDER BY id");

    public Task<List<ItemMaestroDto>> GetTurnosAsync() =>
        ListarAsync("SELECT id, turno_nombre FROM turno ORDER BY id");

    /// <summary>Edificios que tienen aulas en la sede (aulas.edificio_id → edificios.id), o todos si no hay sede.</summary>
    public Task<List<ItemMaestroDto>> GetEdificiosAsync(int? sedeId) =>
        ListarAsync(@"SELECT e.id, e.edificio_nombre FROM edificios e
                      WHERE e.id IN (SELECT a.edificio_id FROM aulas a
                                     WHERE a.edificio_id IS NOT NULL AND (@sede::int IS NULL OR a.sede_id = @sede))
                      ORDER BY e.id", sedeId);

    /// <summary>Facultades: apodos distintos de materia_facultad (la tabla facultad todavía está vacía).</summary>
    public Task<List<string>> GetFacultadesAsync() =>
        ListarTextosAsync(@"SELECT DISTINCT facultad_apodo FROM materia_facultad
                            WHERE facultad_apodo IS NOT NULL ORDER BY 1", null);

    /// <summary>
    /// Nombres de materia distintos. Con facultad, solo los vinculados a ella en materia_facultad (por nombre);
    /// con sede, solo las filas de materia vinculadas a esa sede en materia_sede (por id).
    /// </summary>
    public Task<List<string>> GetMateriasAsync(string? facultad, int? sedeId) =>
        ListarTextosAsync(@"SELECT DISTINCT m.materia_nombre FROM materia m
                            WHERE m.materia_nombre IS NOT NULL
                              AND (@facultad::text IS NULL OR EXISTS (
                                   SELECT 1 FROM materia_facultad mf
                                   WHERE mf.materia_nombre = m.materia_nombre AND mf.facultad_apodo = @facultad))
                              AND (@sede::int IS NULL OR EXISTS (
                                   SELECT 1 FROM materia_sede ms
                                   WHERE ms.materia_id = m.id AND ms.sede_id = @sede))
                            ORDER BY 1", facultad, sedeId);

    /// <summary>
    /// Pisos distintos (aulas.aula_piso, tal cual están cargados) de las aulas de la sede: primero los numéricos en
    /// orden (0, 1, 2...), después los de texto (A, B, P, s, SUM...).
    /// </summary>
    public Task<List<string>> GetPisosAsync(int sedeId, int? edificioId) =>
        ListarTextosAsync(@"SELECT piso FROM (
                                SELECT DISTINCT TRIM(a.aula_piso) AS piso FROM aulas a
                                WHERE NULLIF(TRIM(a.aula_piso), '') IS NOT NULL AND a.sede_id = @sede
                                  AND (@edificio::int IS NULL OR a.edificio_id = @edificio)) p
                            ORDER BY (piso !~ '^[0-9]+$'),
                                     CASE WHEN piso ~ '^[0-9]+$' THEN piso::int END,
                                     piso", null, sedeId, edificioId);

    /// <summary>
    /// Tipos de aula (tabla aula_tipo) que tienen aulas, de la sede si se indica. El valor es aula_tipo.id; los
    /// nombres vienen con espacios sobrantes en la base, por eso el TRIM.
    /// </summary>
    public Task<List<ItemMaestroDto>> GetTiposAulaAsync(int? sedeId) =>
        ListarAsync(@"SELECT t.id, TRIM(t.aula_tipo) FROM aula_tipo t
                      WHERE NULLIF(TRIM(t.aula_tipo), '') IS NOT NULL
                        AND EXISTS (SELECT 1 FROM aulas a
                                    WHERE a.aula_tipo_id = t.id AND (@sede::int IS NULL OR a.sede_id = @sede))
                      ORDER BY TRIM(t.aula_tipo)", sedeId);

    /// <summary>
    /// Aulas con sede (sedes), edificio (edificios) y tipo (aula_tipo) resueltos. Se filtran por sede, piso, edificio y
    /// tipo si se indican; sin sede vienen todas, incluidas las que no tienen sede cargada. Orden: sede, edificio, nombre.
    /// </summary>
    public async Task<List<AulaMaestroDto>> GetAulasAsync(int? sedeId, string? piso, int? edificioId, int? tipoId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT a.id, a.aula_nombre, a.aula_capacidad_real, TRIM(a.aula_piso),
                   a.sede_id, s.nombre_sede, a.edificio_id, e.edificio_nombre, a.aula_tipo_id, TRIM(t.aula_tipo)
            FROM aulas a
            LEFT JOIN sedes s ON s.id = a.sede_id
            LEFT JOIN edificios e ON e.id = a.edificio_id
            LEFT JOIN aula_tipo t ON t.id = a.aula_tipo_id
            WHERE (@sede::int IS NULL OR a.sede_id = @sede)
              AND (@piso::text IS NULL OR TRIM(a.aula_piso) = @piso)
              AND (@edificio::int IS NULL OR a.edificio_id = @edificio)
              AND (@tipo::int IS NULL OR a.aula_tipo_id = @tipo)
            ORDER BY s.id, e.edificio_nombre, a.aula_nombre", conn);
        cmd.Parameters.AddWithValue("@sede", (object?)sedeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@piso", (object?)piso ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@edificio", (object?)edificioId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tipo", (object?)tipoId ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync();

        var aulas = new List<AulaMaestroDto>();
        while (await reader.ReadAsync())
        {
            string? Texto(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);
            aulas.Add(new AulaMaestroDto
            {
                Id = reader.GetInt32(0),
                Nombre = Texto(1) ?? string.Empty,
                Capacidad = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                Piso = Texto(3),
                SedeId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                Sede = Texto(5),
                EdificioId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                Edificio = Texto(7),
                TipoId = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Tipo = Texto(9),
            });
        }
        return aulas;
    }

    private async Task<List<string>> ListarTextosAsync(string sql, string? facultad, int? sedeId = null, int? edificioId = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@facultad", (object?)facultad ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sede", (object?)sedeId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@edificio", (object?)edificioId ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync();

        var textos = new List<string>();
        while (await reader.ReadAsync())
            textos.Add(reader.GetString(0));
        return textos;
    }

    private async Task<List<ItemMaestroDto>> ListarAsync(string sql, int? sedeId = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sede", (object?)sedeId ?? DBNull.Value);
        await using var reader = await cmd.ExecuteReaderAsync();

        var items = new List<ItemMaestroDto>();
        while (await reader.ReadAsync())
            items.Add(new ItemMaestroDto { Id = reader.GetInt32(0), Nombre = reader.GetString(1) });
        return items;
    }
}
