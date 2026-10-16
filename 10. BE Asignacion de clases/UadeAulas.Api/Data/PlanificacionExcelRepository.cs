using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace UadeAulas.Api.Data;

public record PlanificacionExcelGuardada(
    string NombreArchivo, DateTime SubidoEn, string SubidoPor, int? Anio, int? Cuatrimestre,
    List<string> Encabezados, List<List<string>> Filas);

/// <summary>Tabla planificacion_excel: la planificación inicial tal cual vino en el Excel (una sola fila).</summary>
public class PlanificacionExcelRepository
{
    private readonly string _connectionString;

    public PlanificacionExcelRepository(IConfiguration configuration)
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

    public async Task<bool> ExisteAsync()
    {
        await using var conn = await GetConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM planificacion_excel)", conn);
        return (bool)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task ReemplazarAsync(
        string nombreArchivo, string usuario, int? anio, int? cuatrimestre, List<string> encabezados, List<List<string>> filas)
    {
        await using var conn = await GetConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var cmdDel = new NpgsqlCommand("DELETE FROM planificacion_excel", conn, tx))
            await cmdDel.ExecuteNonQueryAsync();
        // Las ediciones de aula apuntan a los Id de la planificación anterior: con una nueva dejan de valer
        await using (var cmdEd = new NpgsqlCommand("DELETE FROM asignacion_aula", conn, tx))
            await cmdEd.ExecuteNonQueryAsync();

        await using (var cmd = new NpgsqlCommand(@"
            INSERT INTO planificacion_excel (nombre_archivo, subido_por, anio, cuatrimestre, encabezados, filas)
            VALUES (@nombre, @usuario, @anio, @cuatrimestre, @encabezados, @filas)", conn, tx))
        {
            cmd.Parameters.AddWithValue("@nombre", nombreArchivo);
            cmd.Parameters.AddWithValue("@usuario", usuario);
            cmd.Parameters.AddWithValue("@anio", (object?)anio ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cuatrimestre", (object?)cuatrimestre ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@encabezados", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(encabezados));
            cmd.Parameters.AddWithValue("@filas", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(filas));
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    /// <summary>Id de la planificación vigente (consulta liviana, para saber si sirve lo que ya está en memoria).</summary>
    public async Task<int?> GetIdVigenteAsync()
    {
        await using var conn = await GetConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT id FROM planificacion_excel ORDER BY id DESC LIMIT 1", conn);
        return await cmd.ExecuteScalarAsync() as int?;
    }

    public async Task<PlanificacionExcelGuardada?> ObtenerAsync()
    {
        await using var conn = await GetConnectionAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT nombre_archivo, subido_en, subido_por, anio, cuatrimestre, encabezados::text, filas::text
            FROM planificacion_excel ORDER BY id DESC LIMIT 1", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new PlanificacionExcelGuardada(
            reader.GetString(0),
            reader.GetDateTime(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3),
            reader.IsDBNull(4) ? null : reader.GetInt32(4),
            JsonSerializer.Deserialize<List<string>>(reader.GetString(5))!,
            JsonSerializer.Deserialize<List<List<string>>>(reader.GetString(6))!);
    }
}
