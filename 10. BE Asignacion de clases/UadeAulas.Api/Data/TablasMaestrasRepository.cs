using Npgsql;
using UadeAulas.Api.Dtos;

namespace UadeAulas.Api.Data;

/// <summary>
/// Tablas maestras que alimentan los desplegables del front (sedes, turno, anio, edificios, tipos de aula, materias, aulas).
/// Todos los filtros aceptan varios valores (selección múltiple): una lista vacía o ausente significa "sin filtro".
/// </summary>
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

    /// <summary>
    /// Códigos de materia (`materia.codigo_materia`, en mayúsculas) de los nombres dados (`materia.materia_nombre`). La
    /// planificación abrevia algunos nombres ("DEC. EMP. EN NEG. INTER."), pero el código siempre coincide.
    /// </summary>
    public async Task<HashSet<string>> GetCodigosDeMateriasAsync(string[] nombres)
    {
        var codigos = new HashSet<string>();
        if (nombres.Length == 0) return codigos;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT DISTINCT UPPER(TRIM(codigo_materia)) FROM materia
            WHERE codigo_materia IS NOT NULL AND TRIM(materia_nombre) = ANY(@n)", conn);
        cmd.Parameters.AddWithValue("@n", nombres.Select(n => n.Trim()).ToArray());
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) codigos.Add(reader.GetString(0));
        return codigos;
    }

    /// <summary>Períodos de la tabla `periodo` (1er Cuatr, 2do Cuatr, Anual, Verano...), en el orden en que se cargaron.</summary>
    public Task<List<ItemMaestroDto>> GetPeriodosAsync() =>
        ListarAsync("SELECT id, TRIM(periodo_nombre) FROM periodo WHERE NULLIF(TRIM(periodo_nombre), '') IS NOT NULL ORDER BY id");

    /// <summary>Años de la tabla `anio` (columna anio): el nombre es el año tal cual, el id es el de la tabla.</summary>
    public Task<List<ItemMaestroDto>> GetAniosAsync() =>
        ListarAsync(@"SELECT id, TRIM(anio::text) FROM anio
                      WHERE NULLIF(TRIM(anio::text), '') IS NOT NULL ORDER BY anio");

    /// <summary>Edificios que tienen aulas en alguna de las sedes (aulas.edificio_id → edificios.id), o todos si no hay sede.</summary>
    public Task<List<ItemMaestroDto>> GetEdificiosAsync(int[]? sedes) =>
        ListarAsync(@"SELECT e.id, e.edificio_nombre FROM edificios e
                      WHERE e.id IN (SELECT a.edificio_id FROM aulas a
                                     WHERE a.edificio_id IS NOT NULL
                                       AND (@sede::int[] IS NULL OR a.sede_id = ANY(@sede)))
                      ORDER BY e.id", sedes);

    /// <summary>Facultades: apodos distintos de materia_facultad (la tabla facultad todavía está vacía).</summary>
    public Task<List<string>> GetFacultadesAsync() =>
        ListarTextosAsync(@"SELECT DISTINCT facultad_apodo FROM materia_facultad
                            WHERE facultad_apodo IS NOT NULL ORDER BY 1");

    /// <summary>
    /// Nombres de materia distintos. Con facultades, solo los vinculados a alguna de ellas en materia_facultad (por
    /// nombre); con sedes, solo las filas de materia vinculadas a alguna de esas sedes en materia_sede (por id).
    /// </summary>
    public Task<List<string>> GetMateriasAsync(string[]? facultades, int[]? sedes) =>
        ListarTextosAsync(@"SELECT DISTINCT m.materia_nombre FROM materia m
                            WHERE m.materia_nombre IS NOT NULL
                              AND (@facultad::text[] IS NULL OR EXISTS (
                                   SELECT 1 FROM materia_facultad mf
                                   WHERE mf.materia_nombre = m.materia_nombre AND mf.facultad_apodo = ANY(@facultad)))
                              AND (@sede::int[] IS NULL OR EXISTS (
                                   SELECT 1 FROM materia_sede ms
                                   WHERE ms.materia_id = m.id AND ms.sede_id = ANY(@sede)))
                            ORDER BY 1", facultades, sedes);

    /// <summary>
    /// Pisos distintos (aulas.aula_piso, tal cual están cargados) de las aulas de las sedes y edificios indicados: primero
    /// los numéricos en orden (0, 1, 2...), después los de texto (A, B, P, s, SUM...).
    /// </summary>
    public Task<List<string>> GetPisosAsync(int[]? sedes, int[]? edificios) =>
        ListarTextosAsync(@"SELECT piso FROM (
                                SELECT DISTINCT TRIM(a.aula_piso) AS piso FROM aulas a
                                WHERE NULLIF(TRIM(a.aula_piso), '') IS NOT NULL
                                  AND (@sede::int[] IS NULL OR a.sede_id = ANY(@sede))
                                  AND (@edificio::int[] IS NULL OR a.edificio_id = ANY(@edificio))) p
                            ORDER BY (piso !~ '^[0-9]+$'),
                                     CASE WHEN piso ~ '^[0-9]+$' THEN piso::int END,
                                     piso", null, sedes, edificios);

    /// <summary>
    /// Tipos de aula (tabla aula_tipo) que tienen aulas, de las sedes indicadas si las hay. El valor es aula_tipo.id; los
    /// nombres vienen con espacios sobrantes en la base, por eso el TRIM.
    /// </summary>
    public Task<List<ItemMaestroDto>> GetTiposAulaAsync(int[]? sedes) =>
        ListarAsync(@"SELECT t.id, TRIM(t.aula_tipo) FROM aula_tipo t
                      WHERE NULLIF(TRIM(t.aula_tipo), '') IS NOT NULL
                        AND EXISTS (SELECT 1 FROM aulas a
                                    WHERE a.aula_tipo_id = t.id AND (@sede::int[] IS NULL OR a.sede_id = ANY(@sede)))
                      ORDER BY TRIM(t.aula_tipo)", sedes);

    /// <summary>
    /// Aulas con sede (sedes), edificio (edificios) y tipo (aula_tipo) resueltos. Se filtran por sedes, pisos, edificios y
    /// tipos si se indican; sin sede vienen todas, incluidas las que no tienen sede cargada. Orden: sede, edificio, nombre.
    /// </summary>
    public async Task<List<AulaMaestroDto>> GetAulasAsync(int[]? sedes, string[]? pisos, int[]? edificios, int[]? tipos)
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
            WHERE (@sede::int[] IS NULL OR a.sede_id = ANY(@sede))
              AND (@piso::text[] IS NULL OR TRIM(a.aula_piso) = ANY(@piso))
              AND (@edificio::int[] IS NULL OR a.edificio_id = ANY(@edificio))
              AND (@tipo::int[] IS NULL OR a.aula_tipo_id = ANY(@tipo))
            ORDER BY s.id, e.edificio_nombre, a.aula_nombre", conn);
        cmd.Parameters.AddWithValue("@sede", Lista(sedes));
        cmd.Parameters.AddWithValue("@piso", Lista(pisos));
        cmd.Parameters.AddWithValue("@edificio", Lista(edificios));
        cmd.Parameters.AddWithValue("@tipo", Lista(tipos));
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

    /// <summary>Un parámetro de lista: sin valores va como NULL, que en el SQL significa "sin filtro".</summary>
    private static object Lista<T>(T[]? valores) => valores is { Length: > 0 } ? valores : DBNull.Value;

    private async Task<List<string>> ListarTextosAsync(
        string sql, string[]? facultades = null, int[]? sedes = null, int[]? edificios = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@facultad", Lista(facultades));
        cmd.Parameters.AddWithValue("@sede", Lista(sedes));
        cmd.Parameters.AddWithValue("@edificio", Lista(edificios));
        await using var reader = await cmd.ExecuteReaderAsync();

        var textos = new List<string>();
        while (await reader.ReadAsync())
            textos.Add(reader.GetString(0));
        return textos;
    }

    private async Task<List<ItemMaestroDto>> ListarAsync(string sql, int[]? sedes = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@sede", Lista(sedes));
        await using var reader = await cmd.ExecuteReaderAsync();

        var items = new List<ItemMaestroDto>();
        while (await reader.ReadAsync())
            items.Add(new ItemMaestroDto { Id = reader.GetInt32(0), Nombre = reader.GetString(1) });
        return items;
    }
}
