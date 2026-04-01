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

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            await using var conn = new NpgsqlConnection(_config.ConnectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<StoreViewModel>> GetStoresAsync()
    {
        await using var conn = new NpgsqlConnection(_config.ConnectionString);
        await conn.OpenAsync();

        const string sql = """
            SELECT id, name, created_at
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
            SELECT authorization_model_id, created_at
            FROM authorization_model
            WHERE store = @storeId
            GROUP BY authorization_model_id, created_at
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
                IsActive = id == _config.AuthorizationModelId
            });
        }
        return models;
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

        // Cursor-based pagination using ulid/inserted_at
        int offset = (filter.Page - 1) * filter.PageSize;

        var sql = $"""
            SELECT _user, relation, object, inserted_at
            FROM tuple
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY inserted_at DESC
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
}
