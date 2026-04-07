using FgaStudio.Web.Models;

namespace FgaStudio.Web.Services;

public interface IFgaService
{
    // ── Connection ────────────────────────────────────────────────────────────
    Task<(bool Success, string? Error)> TestConnectionAsync();

    // ── Stores ────────────────────────────────────────────────────────────────
    Task<List<StoreViewModel>> GetStoresAsync();
    Task<StoreViewModel> CreateStoreAsync(string name);
    Task DeleteStoreAsync(string storeId);

    // ── Authorization models ──────────────────────────────────────────────────
    Task<List<AuthorizationModelViewModel>> GetAuthorizationModelsAsync(string storeId);
    Task<AuthorizationModelDetailViewModel?> GetAuthorizationModelAsync(string storeId, string modelId);

    // ── Tuples ────────────────────────────────────────────────────────────────
    Task<int> CountTuplesAsync(string storeId, TupleFilter filter);
    Task<(List<TupleViewModel> Tuples, string? ContinuationToken)> ReadTuplesAsync(
        string storeId, string modelId, TupleFilter filter, string? continuationToken = null);
    Task WriteTupleAsync(string storeId, string modelId, TupleKey tuple);
    Task DeleteTupleAsync(string storeId, string modelId, TupleKey tuple);

    // ── Changelog ─────────────────────────────────────────────────────────────
    Task<(List<TupleChangeViewModel> Changes, string? ContinuationToken)> ReadChangesAsync(
        string storeId, string? type, int pageSize, string? continuationToken);

    // ── Relationship queries ──────────────────────────────────────────────────
    Task<(bool? Allowed, string? Error)> CheckAsync(string storeId, string modelId, TupleKey tuple);
    Task<(string? TreeJson, string? Error)> ExpandAsync(string storeId, string modelId, string relation, string obj);
    Task<(List<string> Objects, string? Error)> ListObjectsAsync(string storeId, string modelId, string user, string relation, string type);
    Task<(List<string> Users, string? Error)> ListUsersAsync(string storeId, string modelId, string obj, string relation, string userType);
}
