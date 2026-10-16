using Npgsql;

namespace UadeAulas.Api.Data;

public record AsignacionAlumnoFila(string Legajo, string Alumno, string ClaseId, short Opcion);

public record ResumenAsignacionAlumnos(int Filas, int Alumnos, int Opcion1, int Opcion2, DateTime? GeneradoEn, string? GeneradoPor);

/// <summary>Tabla asignacion_alumno: alumnos distribuidos en las clases (opción 1 = primera elección, 2 = segunda).</summary>
public class AsignacionAlumnoRepository
{
    private readonly string _connectionString;

    public AsignacionAlumnoRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found");
    }

    /// <summary>Reemplaza todo el contenido por las filas dadas (una sola transacción).</summary>
    public async Task ReemplazarAsync(IEnumerable<AsignacionAlumnoFila> filas, string usuario)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var del = new NpgsqlCommand("DELETE FROM asignacion_alumno", conn, tx))
            await del.ExecuteNonQueryAsync();

        await using (var import = await conn.BeginBinaryImportAsync(
            "COPY asignacion_alumno (legajo, alumno, clase_id, opcion, asignado_por) FROM STDIN (FORMAT BINARY)"))
        {
            foreach (var f in filas)
            {
                await import.StartRowAsync();
                await import.WriteAsync(f.Legajo, NpgsqlTypes.NpgsqlDbType.Varchar);
                await import.WriteAsync(f.Alumno, NpgsqlTypes.NpgsqlDbType.Varchar);
                await import.WriteAsync(f.ClaseId, NpgsqlTypes.NpgsqlDbType.Varchar);
                await import.WriteAsync(f.Opcion, NpgsqlTypes.NpgsqlDbType.Smallint);
                await import.WriteAsync(usuario, NpgsqlTypes.NpgsqlDbType.Varchar);
            }
            await import.CompleteAsync();
        }

        await tx.CommitAsync();
    }

    /// <summary>Cantidad de alumnos por clase en primera y segunda opción.</summary>
    public async Task<Dictionary<string, (int Primera, int Segunda)>> GetConteosAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT clase_id, COUNT(*) FILTER (WHERE opcion = 1), COUNT(*) FILTER (WHERE opcion = 2)
            FROM asignacion_alumno GROUP BY clase_id", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var resultado = new Dictionary<string, (int, int)>();
        while (await reader.ReadAsync())
            resultado[reader.GetString(0)] = ((int)reader.GetInt64(1), (int)reader.GetInt64(2));
        return resultado;
    }

    public async Task<ResumenAsignacionAlumnos> GetResumenAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT COUNT(*), COUNT(DISTINCT legajo), COUNT(*) FILTER (WHERE opcion = 1), COUNT(*) FILTER (WHERE opcion = 2),
                   MAX(asignado_en), (SELECT asignado_por FROM asignacion_alumno ORDER BY asignado_en DESC LIMIT 1)
            FROM asignacion_alumno", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        return new ResumenAsignacionAlumnos(
            (int)reader.GetInt64(0), (int)reader.GetInt64(1), (int)reader.GetInt64(2), (int)reader.GetInt64(3),
            reader.IsDBNull(4) ? null : reader.GetDateTime(4), reader.IsDBNull(5) ? null : reader.GetString(5));
    }
}
