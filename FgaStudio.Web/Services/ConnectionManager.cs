using FgaStudio.Web.Models;
using Microsoft.Data.Sqlite;

namespace FgaStudio.Web.Services;

public class ConnectionManager
{
    public string DbPath { get; }

    public ConnectionManager(IWebHostEnvironment env)
    {
        DbPath = Path.Combine(env.ContentRootPath, "fgastudio.db");
        InitializeDb();
    }

    // -------------------------------------------------------------------------
    // Schema
    // -------------------------------------------------------------------------

    private void InitializeDb()
    {
        using var conn = Connect();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS connections (
                name                  TEXT PRIMARY KEY,
                type                  INTEGER NOT NULL DEFAULT 0,
                api_url               TEXT,
                auth_method           INTEGER NOT NULL DEFAULT 0,
                api_token             TEXT,
                client_id             TEXT,
                client_secret         TEXT,
                api_audience          TEXT,
                api_token_issuer      TEXT,
                connection_string     TEXT,
                store_id              TEXT,
                authorization_model_id TEXT
            );
            CREATE TABLE IF NOT EXISTS settings (
                key   TEXT PRIMARY KEY,
                value TEXT
            );
            CREATE TABLE IF NOT EXISTS tuple_cache (
                id         INTEGER PRIMARY KEY AUTOINCREMENT,
                connection TEXT NOT NULL,
                store_id   TEXT NOT NULL,
                user       TEXT NOT NULL,
                relation   TEXT NOT NULL,
                object     TEXT NOT NULL,
                timestamp  TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_tuple_cache_lookup
                ON tuple_cache (connection, store_id);
            CREATE TABLE IF NOT EXISTS tuple_cache_meta (
                connection   TEXT NOT NULL,
                store_id     TEXT NOT NULL,
                cached_at    TEXT NOT NULL,
                total_tuples INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (connection, store_id)
            );
            """;
        cmd.ExecuteNonQuery();
    }

    // -------------------------------------------------------------------------
    // Read
    // -------------------------------------------------------------------------

    public IReadOnlyList<ConnectionConfig> GetAll()
    {
        using var conn = Connect();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM connections ORDER BY name";
        using var reader = cmd.ExecuteReader();
        var list = new List<ConnectionConfig>();
        while (reader.Read()) list.Add(Map(reader));
        return list;
    }

    public ConnectionConfig? GetActive()
    {
        var name = GetSetting("active_connection");
        return name is null ? null : GetByName(name);
    }

    public ConnectionConfig? GetByName(string name)
    {
        using var conn = Connect();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT * FROM connections WHERE name = $name";
        cmd.Parameters.AddWithValue("$name", name);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? Map(reader) : null;
    }

    // -------------------------------------------------------------------------
    // Write
    // -------------------------------------------------------------------------

    public async Task SaveConnectionAsync(ConnectionConfig c)
    {
        await using var conn = Connect();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT OR REPLACE INTO connections
                (name, type, api_url, auth_method, api_token, client_id, client_secret,
                 api_audience, api_token_issuer, connection_string, store_id, authorization_model_id)
            VALUES
                ($name, $type, $api_url, $auth_method, $api_token, $client_id, $client_secret,
                 $api_audience, $api_token_issuer, $connection_string, $store_id, $authorization_model_id)
            """;
        BindParams(cmd, c);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteConnectionAsync(string name)
    {
        await using var conn = Connect();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM connections WHERE name = $name";
        cmd.Parameters.AddWithValue("$name", name);
        await cmd.ExecuteNonQueryAsync();

        if (GetSetting("active_connection") == name)
        {
            var next = GetAll().FirstOrDefault()?.Name;
            await SetSettingAsync("active_connection", next);
        }
    }

    public async Task SetActiveConnectionAsync(string name)
        => await SetSettingAsync("active_connection", name);

    public async Task UpdateStoreContextAsync(string connectionName, string storeId, string modelId)
    {
        await using var conn = Connect();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            UPDATE connections
            SET store_id = $storeId, authorization_model_id = $modelId
            WHERE name = $name
            """;
        cmd.Parameters.AddWithValue("$name", connectionName);
        cmd.Parameters.AddWithValue("$storeId", storeId);
        cmd.Parameters.AddWithValue("$modelId", modelId);
        await cmd.ExecuteNonQueryAsync();
    }

    // -------------------------------------------------------------------------
    // Service factory
    // -------------------------------------------------------------------------

    public IFgaService? BuildService(ConnectionConfig config) => config.Type switch
    {
        ConnectionType.Url      => new FgaHttpService(config),
        ConnectionType.Database => new FgaDbService(config),
        _ => null
    };

    public IFgaService? BuildActiveService()
    {
        var active = GetActive();
        return active is null ? null : BuildService(active);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private SqliteConnection Connect()
    {
        var conn = new SqliteConnection($"Data Source={DbPath}");
        conn.Open();
        return conn;
    }

    private string? GetSetting(string key)
    {
        using var conn = Connect();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key";
        cmd.Parameters.AddWithValue("$key", key);
        return cmd.ExecuteScalar() as string;
    }

    private async Task SetSettingAsync(string key, string? value)
    {
        await using var conn = Connect();
        await using var cmd = conn.CreateCommand();
        if (value is null)
        {
            cmd.CommandText = "DELETE FROM settings WHERE key = $key";
            cmd.Parameters.AddWithValue("$key", key);
        }
        else
        {
            cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES ($key, $value)";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
        }
        await cmd.ExecuteNonQueryAsync();
    }

    private static void BindParams(SqliteCommand cmd, ConnectionConfig c)
    {
        cmd.Parameters.AddWithValue("$name",                   c.Name);
        cmd.Parameters.AddWithValue("$type",                   (int)c.Type);
        cmd.Parameters.AddWithValue("$api_url",                (object?)c.ApiUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$auth_method",            (int)c.AuthMethod);
        cmd.Parameters.AddWithValue("$api_token",              (object?)c.ApiToken ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$client_id",              (object?)c.ClientId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$client_secret",          (object?)c.ClientSecret ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$api_audience",           (object?)c.ApiAudience ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$api_token_issuer",       (object?)c.ApiTokenIssuer ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$connection_string",      (object?)c.ConnectionString ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$store_id",               (object?)c.StoreId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$authorization_model_id", (object?)c.AuthorizationModelId ?? DBNull.Value);
    }

    private static ConnectionConfig Map(SqliteDataReader r) => new()
    {
        Name                  = r.GetString(r.GetOrdinal("name")),
        Type                  = (ConnectionType)r.GetInt32(r.GetOrdinal("type")),
        ApiUrl                = r.IsDBNull(r.GetOrdinal("api_url"))                ? null : r.GetString(r.GetOrdinal("api_url")),
        AuthMethod            = (AuthMethod)r.GetInt32(r.GetOrdinal("auth_method")),
        ApiToken              = r.IsDBNull(r.GetOrdinal("api_token"))              ? null : r.GetString(r.GetOrdinal("api_token")),
        ClientId              = r.IsDBNull(r.GetOrdinal("client_id"))              ? null : r.GetString(r.GetOrdinal("client_id")),
        ClientSecret          = r.IsDBNull(r.GetOrdinal("client_secret"))          ? null : r.GetString(r.GetOrdinal("client_secret")),
        ApiAudience           = r.IsDBNull(r.GetOrdinal("api_audience"))           ? null : r.GetString(r.GetOrdinal("api_audience")),
        ApiTokenIssuer        = r.IsDBNull(r.GetOrdinal("api_token_issuer"))       ? null : r.GetString(r.GetOrdinal("api_token_issuer")),
        ConnectionString      = r.IsDBNull(r.GetOrdinal("connection_string"))      ? null : r.GetString(r.GetOrdinal("connection_string")),
        StoreId               = r.IsDBNull(r.GetOrdinal("store_id"))               ? null : r.GetString(r.GetOrdinal("store_id")),
        AuthorizationModelId  = r.IsDBNull(r.GetOrdinal("authorization_model_id")) ? null : r.GetString(r.GetOrdinal("authorization_model_id")),
    };
}
