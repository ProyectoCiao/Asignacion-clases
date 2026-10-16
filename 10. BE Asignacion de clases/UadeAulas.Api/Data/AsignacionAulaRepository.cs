using Npgsql;

namespace UadeAulas.Api.Data;

public record AsignacionAula(string ClaseId, int? AulaId, string ModificadoPor, DateTime ModificadoEn);

/// <summary>Tabla asignacion_aula: el aula elegida por clase (clave = "Id" del Excel). El Excel original no se toca.</summary>
public class AsignacionAulaRepository
{
    private readonly string _connectionString;

    public AsignacionAulaRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found");
    }

    public async Task<Dictionary<string, AsignacionAula>> GetTodasAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT clase_id, aula_id, modificado_por, modificado_en FROM asignacion_aula", conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var resultado = new Dictionary<string, AsignacionAula>();
        while (await reader.ReadAsync())
            resultado[reader.GetString(0)] = new AsignacionAula(
                reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetInt32(1), reader.GetString(2), reader.GetDateTime(3));
        return resultado;
    }

    public async Task GuardarAsync(string claseId, int? aulaId, string usuario, string usuarioReal)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO asignacion_aula (clase_id, aula_id, modificado_por, modificado_por_real, modificado_en)
            VALUES (@clase, @aula, @usuario, @real, now())
            ON CONFLICT (clase_id) DO UPDATE
               SET aula_id = EXCLUDED.aula_id, modificado_por = EXCLUDED.modificado_por,
                   modificado_por_real = EXCLUDED.modificado_por_real, modificado_en = now()", conn);
        cmd.Parameters.AddWithValue("@clase", claseId);
        cmd.Parameters.AddWithValue("@aula", (object?)aulaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@usuario", usuario);
        cmd.Parameters.AddWithValue("@real", usuarioReal);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Quita la edición: la clase vuelve al aula del Excel. Devuelve false si no tenía edición.</summary>
    public async Task<bool> QuitarAsync(string claseId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM asignacion_aula WHERE clase_id = @clase", conn);
        cmd.Parameters.AddWithValue("@clase", claseId);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }
}
