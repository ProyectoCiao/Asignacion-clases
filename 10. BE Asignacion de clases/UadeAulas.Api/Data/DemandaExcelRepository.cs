using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace UadeAulas.Api.Data;

public record DemandaExcelGuardada(
    int Id, string NombreArchivo, DateTime SubidoEn, string SubidoPor, List<string> Encabezados, List<List<string>> Filas);

/// <summary>Tabla demanda_excel: el último Excel de demanda subido, tal cual (una sola fila). Provisorio hasta la API.</summary>
public class DemandaExcelRepository
{
    private readonly string _connectionString;

    public DemandaExcelRepository(IConfiguration configuration)
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

    public async Task ReemplazarAsync(string nombreArchivo, string usuario, List<string> encabezados, List<List<string>> filas)
    {
        await using var conn = await GetConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using (var cmdDel = new NpgsqlCommand("DELETE FROM demanda_excel", conn, tx))
            await cmdDel.ExecuteNonQueryAsync();

        await using (var cmd = new NpgsqlCommand(@"
            INSERT INTO demanda_excel (nombre_archivo, subido_por, encabezados, filas)
            VALUES (@nombre, @usuario, @encabezados, @filas)", conn, tx))
        {
            cmd.Parameters.AddWithValue("@nombre", nombreArchivo);
            cmd.Parameters.AddWithValue("@usuario", usuario);
            cmd.Parameters.AddWithValue("@encabezados", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(encabezados));
            cmd.Parameters.AddWithValue("@filas", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(filas));
            await cmd.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
    }

    /// <summary>Agrega una fila (una inscripción) al final del Excel vigente. Devuelve false si no hay ninguno cargado.</summary>
    public async Task<bool> AgregarFilaAsync(List<string> fila)
    {
        await using var conn = await GetConnectionAsync();
        await using var cmd = new NpgsqlCommand(@"
            UPDATE demanda_excel SET filas = filas || @fila::jsonb
            WHERE id = (SELECT MAX(id) FROM demanda_excel)", conn);
        cmd.Parameters.AddWithValue("@fila", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(new[] { fila }));
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>Id del Excel vigente (consulta liviana, para saber si sirve lo que ya está en memoria).</summary>
    public async Task<int?> GetIdVigenteAsync()
    {
        await using var conn = await GetConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT id FROM demanda_excel ORDER BY id DESC LIMIT 1", conn);
        return await cmd.ExecuteScalarAsync() as int?;
    }

    public async Task<DemandaExcelGuardada?> ObtenerAsync()
    {
        await using var conn = await GetConnectionAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, nombre_archivo, subido_en, subido_por, encabezados::text, filas::text
            FROM demanda_excel ORDER BY id DESC LIMIT 1", conn);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        return new DemandaExcelGuardada(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetDateTime(2),
            reader.GetString(3),
            JsonSerializer.Deserialize<List<string>>(reader.GetString(4))!,
            JsonSerializer.Deserialize<List<List<string>>>(reader.GetString(5))!);
    }
}
