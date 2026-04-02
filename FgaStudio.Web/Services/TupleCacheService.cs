using FgaStudio.Web.Models;
using Microsoft.Data.Sqlite;

namespace FgaStudio.Web.Services;

public record CacheMeta(string ConnectionName, string StoreId, DateTime CachedAt, int TotalTuples);

public record CachedTupleResult(List<TupleViewModel> Tuples, int TotalCount, int TotalPages);

public class TupleCacheService
{
    private readonly string _dbPath;

    public TupleCacheService(ConnectionManager connectionManager)
    {
        _dbPath = connectionManager.DbPath;
    }

    // -------------------------------------------------------------------------
    // Metadata
    // -------------------------------------------------------------------------

    public async Task<CacheMeta?> GetMetaAsync(string connectionName, string storeId)
    {
        await using var conn = Connect();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT cached_at, total_tuples
            FROM tuple_cache_meta
            WHERE connection = $conn AND store_id = $storeId
            """;
        cmd.Parameters.AddWithValue("$conn", connectionName);
        cmd.Parameters.AddWithValue("$storeId", storeId);
        await using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return new CacheMeta(
            connectionName,
            storeId,
            DateTime.Parse(reader.GetString(0), null, System.Globalization.DateTimeStyles.RoundtripKind),
            reader.GetInt32(1));
    }

    // -------------------------------------------------------------------------
    // Refresh
    // -------------------------------------------------------------------------

    public async Task RefreshAsync(
        string connectionName, string storeId, string modelId,
        IFgaService httpService, CancellationToken cancellationToken = default)
    {
        // Fetch all tuples from the API by following continuation tokens.
        var all = new List<TupleViewModel>();
        string? token = null;
        var batchFilter = new TupleFilter { PageSize = 100 };

        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (batch, nextToken) = await httpService.ReadTuplesAsync(
                storeId, modelId, batchFilter, token);
            all.AddRange(batch);
            token = string.IsNullOrEmpty(nextToken) ? null : nextToken;
        }
        while (token != null);

        // Atomically replace the cache for this (connection, store).
        await using var conn = Connect();
        await using var tx = await conn.BeginTransactionAsync(cancellationToken);

        await using (var del = conn.CreateCommand())
        {
            del.Transaction = (SqliteTransaction)tx;
            del.CommandText = "DELETE FROM tuple_cache WHERE connection = $conn AND store_id = $storeId";
            del.Parameters.AddWithValue("$conn", connectionName);
            del.Parameters.AddWithValue("$storeId", storeId);
            await del.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var ins = conn.CreateCommand())
        {
            ins.Transaction = (SqliteTransaction)tx;
            ins.CommandText = """
                INSERT INTO tuple_cache (connection, store_id, user, relation, object, timestamp)
                VALUES ($conn, $storeId, $user, $relation, $object, $ts)
                """;
            var pConn     = ins.Parameters.Add("$conn",     SqliteType.Text);
            var pStore    = ins.Parameters.Add("$storeId",  SqliteType.Text);
            var pUser     = ins.Parameters.Add("$user",     SqliteType.Text);
            var pRelation = ins.Parameters.Add("$relation", SqliteType.Text);
            var pObject   = ins.Parameters.Add("$object",   SqliteType.Text);
            var pTs       = ins.Parameters.Add("$ts",       SqliteType.Text);

            pConn.Value  = connectionName;
            pStore.Value = storeId;

            foreach (var t in all)
            {
                pUser.Value     = t.User;
                pRelation.Value = t.Relation;
                pObject.Value   = t.Object;
                pTs.Value       = t.Timestamp.HasValue
                    ? t.Timestamp.Value.ToString("O")
                    : (object)DBNull.Value;
                await ins.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await using (var meta = conn.CreateCommand())
        {
            meta.Transaction = (SqliteTransaction)tx;
            meta.CommandText = """
                INSERT OR REPLACE INTO tuple_cache_meta (connection, store_id, cached_at, total_tuples)
                VALUES ($conn, $storeId, $cachedAt, $total)
                """;
            meta.Parameters.AddWithValue("$conn",     connectionName);
            meta.Parameters.AddWithValue("$storeId",  storeId);
            meta.Parameters.AddWithValue("$cachedAt", DateTime.UtcNow.ToString("O"));
            meta.Parameters.AddWithValue("$total",    all.Count);
            await meta.ExecuteNonQueryAsync(cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    // -------------------------------------------------------------------------
    // Query
    // -------------------------------------------------------------------------

    public async Task<CachedTupleResult> QueryAsync(
        string connectionName, string storeId, TupleFilter filter)
    {
        await using var conn = Connect();

        var conditions = new List<string>
        {
            "connection = $conn",
            "store_id   = $storeId"
        };
        if (!string.IsNullOrWhiteSpace(filter.User))     conditions.Add("user     = $user");
        if (!string.IsNullOrWhiteSpace(filter.Relation)) conditions.Add("relation = $relation");
        if (!string.IsNullOrWhiteSpace(filter.Object))   conditions.Add("object   = $object");

        var where = string.Join(" AND ", conditions);

        // Total count (fast path from meta when no filter is active, COUNT otherwise).
        int total;
        bool hasFilter = !string.IsNullOrWhiteSpace(filter.User)
                      || !string.IsNullOrWhiteSpace(filter.Relation)
                      || !string.IsNullOrWhiteSpace(filter.Object);

        if (hasFilter)
        {
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM tuple_cache WHERE {where}";
            BindFilterParams(countCmd, connectionName, storeId, filter);
            total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
        }
        else
        {
            await using var metaCmd = conn.CreateCommand();
            metaCmd.CommandText = "SELECT total_tuples FROM tuple_cache_meta WHERE connection = $conn AND store_id = $storeId";
            metaCmd.Parameters.AddWithValue("$conn",    connectionName);
            metaCmd.Parameters.AddWithValue("$storeId", storeId);
            total = Convert.ToInt32(await metaCmd.ExecuteScalarAsync() ?? 0);
        }

        int totalPages = total == 0 ? 1 : (int)Math.Ceiling((double)total / filter.PageSize);
        int page       = Math.Clamp(filter.Page, 1, totalPages);
        int offset     = (page - 1) * filter.PageSize;

        bool desc = filter.SortDir?.ToLowerInvariant() != "asc";
        string orderBy = filter.SortBy?.ToLowerInvariant() switch
        {
            "user"      => $"user     {(desc ? "DESC" : "ASC")}",
            "relation"  => $"relation {(desc ? "DESC" : "ASC")}",
            "object"    => $"object   {(desc ? "DESC" : "ASC")}",
            _           => $"timestamp {(desc ? "DESC" : "ASC")}",
        };

        await using var dataCmd = conn.CreateCommand();
        dataCmd.CommandText = $"""
            SELECT user, relation, object, timestamp
            FROM tuple_cache
            WHERE {where}
            ORDER BY {orderBy}
            LIMIT $limit OFFSET $offset
            """;
        BindFilterParams(dataCmd, connectionName, storeId, filter);
        dataCmd.Parameters.AddWithValue("$limit",  filter.PageSize);
        dataCmd.Parameters.AddWithValue("$offset", offset);

        await using var reader = await dataCmd.ExecuteReaderAsync();
        var tuples = new List<TupleViewModel>();
        while (await reader.ReadAsync())
        {
            tuples.Add(new TupleViewModel
            {
                User      = reader.GetString(0),
                Relation  = reader.GetString(1),
                Object    = reader.GetString(2),
                Timestamp = reader.IsDBNull(3) ? null
                    : DateTime.Parse(reader.GetString(3), null,
                        System.Globalization.DateTimeStyles.RoundtripKind),
            });
        }

        return new CachedTupleResult(tuples, total, totalPages);
    }

    // -------------------------------------------------------------------------
    // Invalidate
    // -------------------------------------------------------------------------

    public async Task InvalidateAsync(string connectionName, string storeId)
    {
        await using var conn = Connect();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            DELETE FROM tuple_cache      WHERE connection = $conn AND store_id = $storeId;
            DELETE FROM tuple_cache_meta WHERE connection = $conn AND store_id = $storeId;
            """;
        cmd.Parameters.AddWithValue("$conn",    connectionName);
        cmd.Parameters.AddWithValue("$storeId", storeId);
        await cmd.ExecuteNonQueryAsync();
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private SqliteConnection Connect()
    {
        var conn = new SqliteConnection($"Data Source={_dbPath}");
        conn.Open();
        return conn;
    }

    private static void BindFilterParams(
        SqliteCommand cmd, string connectionName, string storeId, TupleFilter filter)
    {
        cmd.Parameters.AddWithValue("$conn",    connectionName);
        cmd.Parameters.AddWithValue("$storeId", storeId);
        if (!string.IsNullOrWhiteSpace(filter.User))     cmd.Parameters.AddWithValue("$user",     filter.User);
        if (!string.IsNullOrWhiteSpace(filter.Relation)) cmd.Parameters.AddWithValue("$relation", filter.Relation);
        if (!string.IsNullOrWhiteSpace(filter.Object))   cmd.Parameters.AddWithValue("$object",   filter.Object);
    }
}
