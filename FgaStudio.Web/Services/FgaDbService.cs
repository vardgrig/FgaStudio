using FgaStudio.Web.Models;
using Npgsql;

namespace FgaStudio.Web.Services;

/// <summary>
/// Connects directly to the OpenFGA PostgreSQL database.
/// Useful when you have DB access but no HTTP API endpoint.
/// Read operations are fully supported; writes use raw SQL matching OpenFGA's schema.
/// </summary>
public class FgaDbService : IFgaService
{
    private readonly ConnectionConfig _config;

    public FgaDbService(ConnectionConfig config)
    {
        _config = config;
    }

    private NpgsqlConnection OpenConnection()
    {
        var conn = new NpgsqlConnection(_config.ConnectionString);
        conn.Open();
        return conn;
    }

    public async Task<int> CountTuplesAsync(string storeId, TupleFilter filter)
    {
        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync();

        var conditions = new List<string> { "store = @storeId", "deleted_at IS NULL" };
        if (!string.IsNullOrWhiteSpace(filter.User))     conditions.Add("_user = @user");
        if (!string.IsNullOrWhiteSpace(filter.Relation)) conditions.Add("relation = @relation");
        if (!string.IsNullOrWhiteSpace(filter.Object))   conditions.Add("object = @object");

        var sql = $"SELECT COUNT(*) FROM tuple WHERE {string.Join(" AND ", conditions)}";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("storeId", storeId);
        if (!string.IsNullOrWhiteSpace(filter.User))     cmd.Parameters.AddWithValue("user", filter.User);
        if (!string.IsNullOrWhiteSpace(filter.Relation)) cmd.Parameters.AddWithValue("relation", filter.Relation);
        if (!string.IsNullOrWhiteSpace(filter.Object))   cmd.Parameters.AddWithValue("object", filter.Object);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<(bool Success, string? Error)> TestConnectionAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(_config.ConnectionString);
            await conn.OpenAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task<List<StoreViewModel>> GetStoresAsync()
    {
        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync();

        const string sql = """
            SELECT id, name, created_at, updated_at
            FROM store
            WHERE deleted_at IS NULL
            ORDER BY created_at DESC
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        var stores = new List<StoreViewModel>();
        while (await reader.ReadAsync())
        {
            stores.Add(new StoreViewModel
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                CreatedAt = reader.IsDBNull(2) ? null : reader.GetDateTime(2),
                UpdatedAt = reader.IsDBNull(3) ? null : reader.GetDateTime(3),
                IsActive = reader.GetString(0) == _config.StoreId
            });
        }
        return stores;
    }

    public async Task<List<AuthorizationModelViewModel>> GetAuthorizationModelsAsync(string storeId)
    {
        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync();

        const string sql = """
            SELECT authorization_model_id, created_at, schema_version
            FROM authorization_model
            WHERE store = @storeId
            GROUP BY authorization_model_id, created_at, schema_version
            ORDER BY created_at DESC
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("storeId", storeId);
        await using var reader = await cmd.ExecuteReaderAsync();

        var models = new List<AuthorizationModelViewModel>();
        while (await reader.ReadAsync())
        {
            var id = reader.GetString(0);
            models.Add(new AuthorizationModelViewModel
            {
                Id = id,
                CreatedAt = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
                SchemaVersion = reader.IsDBNull(2) ? null : reader.GetString(2),
                IsActive = id == _config.AuthorizationModelId
            });
        }
        return models;
    }

    public async Task<AuthorizationModelDetailViewModel?> GetAuthorizationModelAsync(string storeId, string modelId)
    {
        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync();

        const string sql = """
            SELECT authorization_model_id, created_at
            FROM authorization_model
            WHERE store = @storeId AND authorization_model_id = @modelId
            LIMIT 1
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("storeId", storeId);
        cmd.Parameters.AddWithValue("modelId", modelId);
        await using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync()) return null;

        return new AuthorizationModelDetailViewModel
        {
            Id = reader.GetString(0),
            CreatedAt = reader.IsDBNull(1) ? null : reader.GetDateTime(1),
            IsActive = reader.GetString(0) == _config.AuthorizationModelId,
            SchemaJson = null,
            TypeDefinitions = []
        };
    }

    public async Task<(List<TupleViewModel> Tuples, string? ContinuationToken)> ReadTuplesAsync(
        string storeId, string modelId, TupleFilter filter, string? continuationToken = null)
    {
        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync();

        var conditions = new List<string> { "store = @storeId", "deleted_at IS NULL" };
        if (!string.IsNullOrWhiteSpace(filter.User)) conditions.Add("_user = @user");
        if (!string.IsNullOrWhiteSpace(filter.Relation)) conditions.Add("relation = @relation");
        if (!string.IsNullOrWhiteSpace(filter.Object)) conditions.Add("object = @object");

        int offset = (filter.Page - 1) * filter.PageSize;
        bool desc = filter.SortDir?.ToLowerInvariant() != "asc";
        string orderBy = filter.SortBy?.ToLowerInvariant() switch
        {
            "user"      => $"_user {(desc ? "DESC" : "ASC")}",
            "relation"  => $"relation {(desc ? "DESC" : "ASC")}",
            "object"    => $"object {(desc ? "DESC" : "ASC")}",
            _           => $"inserted_at {(desc ? "DESC" : "ASC")}",  // default: timestamp
        };

        var sql = $"""
            SELECT _user, relation, object, inserted_at
            FROM tuple
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY {orderBy}
            LIMIT @pageSize OFFSET @offset
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("storeId", storeId);
        if (!string.IsNullOrWhiteSpace(filter.User)) cmd.Parameters.AddWithValue("user", filter.User);
        if (!string.IsNullOrWhiteSpace(filter.Relation)) cmd.Parameters.AddWithValue("relation", filter.Relation);
        if (!string.IsNullOrWhiteSpace(filter.Object)) cmd.Parameters.AddWithValue("object", filter.Object);
        cmd.Parameters.AddWithValue("pageSize", filter.PageSize);
        cmd.Parameters.AddWithValue("offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync();
        var tuples = new List<TupleViewModel>();
        while (await reader.ReadAsync())
        {
            tuples.Add(new TupleViewModel
            {
                User = reader.GetString(0),
                Relation = reader.GetString(1),
                Object = reader.GetString(2),
                Timestamp = reader.IsDBNull(3) ? null : reader.GetDateTime(3)
            });
        }

        return (tuples, null); // DB mode uses offset pagination, no continuation token
    }

    public async Task WriteTupleAsync(string storeId, string modelId, TupleKey tuple)
    {
        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync();

        // OpenFGA uses ULIDs for primary keys; use a timestamp-based surrogate here
        const string sql = """
            INSERT INTO tuple (store, _user, relation, object, authorization_model_id, inserted_at)
            VALUES (@store, @user, @relation, @object, @modelId, NOW())
            ON CONFLICT DO NOTHING
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("store", storeId);
        cmd.Parameters.AddWithValue("user", tuple.User);
        cmd.Parameters.AddWithValue("relation", tuple.Relation);
        cmd.Parameters.AddWithValue("object", tuple.Object);
        cmd.Parameters.AddWithValue("modelId", modelId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTupleAsync(string storeId, string modelId, TupleKey tuple)
    {
        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync();

        const string sql = """
            UPDATE tuple
            SET deleted_at = NOW()
            WHERE store = @store AND _user = @user AND relation = @relation AND object = @object
              AND deleted_at IS NULL
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("store", storeId);
        cmd.Parameters.AddWithValue("user", tuple.User);
        cmd.Parameters.AddWithValue("relation", tuple.Relation);
        cmd.Parameters.AddWithValue("object", tuple.Object);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Changelog ─────────────────────────────────────────────────────────────

    public async Task<(List<TupleChangeViewModel> Changes, string? ContinuationToken)> ReadChangesAsync(
        string storeId, string? type, int pageSize, string? continuationToken)
    {
        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync();

        int offset = int.TryParse(continuationToken, out int p) ? p : 0;

        var conditions = new List<string> { "store = @storeId" };
        if (!string.IsNullOrWhiteSpace(type)) conditions.Add("object_type = @type");

        var sql = $"""
            SELECT _user, relation, object_type || ':' || object_id AS object,
                   operation, inserted_at
            FROM changelog
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY inserted_at DESC
            LIMIT @pageSize OFFSET @offset
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("storeId", storeId);
        if (!string.IsNullOrWhiteSpace(type)) cmd.Parameters.AddWithValue("type", type);
        cmd.Parameters.AddWithValue("pageSize", pageSize);
        cmd.Parameters.AddWithValue("offset", offset);

        await using var reader = await cmd.ExecuteReaderAsync();
        var changes = new List<TupleChangeViewModel>();
        while (await reader.ReadAsync())
        {
            changes.Add(new TupleChangeViewModel
            {
                User      = reader.GetString(0),
                Relation  = reader.GetString(1),
                Object    = reader.GetString(2),
                Operation = reader.GetInt32(3) == 1 ? "write" : "delete",
                Timestamp = reader.IsDBNull(4) ? null : reader.GetDateTime(4)
            });
        }

        string? nextToken = changes.Count == pageSize ? (offset + pageSize).ToString() : null;
        return (changes, nextToken);
    }

    // ── Relationship queries — not supported in DB mode ───────────────────────

    private const string DbQueryNotSupported =
        "Relationship queries (Check, Expand, ListObjects, ListUsers) require the OpenFGA " +
        "evaluation engine and are not available in Direct Database mode. Switch to an HTTP API connection.";

    public Task<(bool? Allowed, string? Error)> CheckAsync(string storeId, string modelId, TupleKey tuple)
        => Task.FromResult<(bool?, string?)>((null, DbQueryNotSupported));

    public Task<(string? TreeJson, string? Error)> ExpandAsync(string storeId, string modelId, string relation, string obj)
        => Task.FromResult<(string?, string?)>((null, DbQueryNotSupported));

    public Task<(List<string> Objects, string? Error)> ListObjectsAsync(string storeId, string modelId, string user, string relation, string type)
        => Task.FromResult<(List<string>, string?)>(([], DbQueryNotSupported));

    public Task<(List<string> Users, string? Error)> ListUsersAsync(string storeId, string modelId, string obj, string relation, string userType)
        => Task.FromResult<(List<string>, string?)>(([], DbQueryNotSupported));

    // ── Store management — not supported in DB mode ───────────────────────────

    public Task<StoreViewModel> CreateStoreAsync(string name)
        => Task.FromException<StoreViewModel>(
            new NotSupportedException("Store creation is not supported in Direct Database mode."));

    public Task DeleteStoreAsync(string storeId)
        => Task.FromException(
            new NotSupportedException("Store deletion is not supported in Direct Database mode."));
}
