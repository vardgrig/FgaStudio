using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.Tuples;

public class IndexModel : PageModel
{
    private readonly ConnectionManager _connectionManager;
    private readonly TupleCacheService _tupleCache;

    public ConnectionConfig? ActiveConnection { get; private set; }
    public string? ActiveConnectionName { get; private set; }
    public string? ActiveStoreId { get; private set; }
    public string? ActiveModelId { get; private set; }
    public bool IsReady { get; private set; }
    public bool IsUrlConnection { get; private set; }

    public TupleFilter Filter { get; private set; } = new();
    public List<TupleViewModel> Tuples { get; private set; } = [];
    public string? Error { get; private set; }

    public string? SortBy { get; private set; }
    public string? SortDir { get; private set; }

    // Pagination
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; }
    public bool HasPrevPage => Filter.Page > 1;
    public bool HasNextPage => Filter.Page < TotalPages;
    public int StartItem => TotalCount == 0 ? 0 : (Filter.Page - 1) * Filter.PageSize + 1;
    public int EndItem => StartItem + Tuples.Count - 1;

    // Cache status (HTTP connections only)
    public bool CacheIsEmpty { get; private set; }
    public DateTime? CachedAt { get; private set; }

    public IndexModel(ConnectionManager connectionManager, TupleCacheService tupleCache)
    {
        _connectionManager = connectionManager;
        _tupleCache = tupleCache;
    }

    public async Task OnGetAsync(
        string? user, string? relation, string? @object,
        int page = 1, int pageSize = 25,
        string? sortBy = null, string? sortDir = null)
    {
        ActiveConnection = _connectionManager.GetActive();
        ActiveConnectionName = ActiveConnection?.Name;
        ActiveStoreId = ActiveConnection?.StoreId;
        ActiveModelId = ActiveConnection?.AuthorizationModelId;

        IsReady = ActiveConnection is not null && !string.IsNullOrEmpty(ActiveStoreId);
        if (!IsReady) return;

        IsUrlConnection = ActiveConnection!.Type == ConnectionType.Url;
        SortBy  = sortBy?.ToLowerInvariant();
        SortDir = sortDir?.ToLowerInvariant() == "asc" ? "asc" : "desc";

        Filter = new TupleFilter
        {
            User     = user,
            Relation = relation,
            Object   = @object,
            Page     = Math.Max(1, page),
            PageSize = pageSize,
            SortBy   = SortBy,
            SortDir  = SortDir,
        };

        var service = _connectionManager.BuildService(ActiveConnection!);
        if (service is null) return;

        try
        {
            if (IsUrlConnection)
            {
                var meta = await _tupleCache.GetMetaAsync(ActiveConnectionName!, ActiveStoreId!);
                if (meta is null)
                {
                    CacheIsEmpty = true;
                    return;
                }

                CachedAt = meta.CachedAt;
                var result = await _tupleCache.QueryAsync(ActiveConnectionName!, ActiveStoreId!, Filter);
                Tuples     = result.Tuples;
                TotalCount = result.TotalCount;
                TotalPages = result.TotalPages;
            }
            else
            {
                // DB: run COUNT and SELECT in parallel.
                var countTask = service.CountTuplesAsync(ActiveStoreId!, Filter);
                var dataTask  = service.ReadTuplesAsync(ActiveStoreId!, ActiveModelId ?? string.Empty, Filter);
                await Task.WhenAll(countTask, dataTask);

                TotalCount = countTask.Result;
                TotalPages = TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / Filter.PageSize);
                Tuples     = dataTask.Result.Tuples;
            }
        }
        catch (Exception ex)
        {
            Error = $"Failed to load tuples: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostRefreshCacheAsync()
    {
        var active = _connectionManager.GetActive();
        if (active is null || active.Type != ConnectionType.Url || string.IsNullOrEmpty(active.StoreId))
            return RedirectToPage();

        var service = _connectionManager.BuildService(active);
        if (service is null) return RedirectToPage();

        try
        {
            await _tupleCache.RefreshAsync(
                active.Name, active.StoreId, active.AuthorizationModelId ?? string.Empty, service);
            TempData["Success"] = "Tuple cache refreshed successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Cache refresh failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string user, string relation, string @object)
    {
        var active = _connectionManager.GetActive();
        if (active is null || string.IsNullOrEmpty(active.StoreId))
        {
            TempData["Error"] = "No active connection or store selected.";
            return RedirectToPage();
        }

        var service = _connectionManager.BuildService(active);
        if (service is null) return RedirectToPage();

        try
        {
            await service.DeleteTupleAsync(active.StoreId, active.AuthorizationModelId ?? string.Empty,
                new TupleKey { User = user, Relation = relation, Object = @object });

            if (active.Type == ConnectionType.Url)
                await _tupleCache.InvalidateAsync(active.Name, active.StoreId);

            TempData["Success"] = "Tuple deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to delete tuple: {ex.Message}";
        }

        return RedirectToPage();
    }

    public string NextSortDir(string column) =>
        column.ToLowerInvariant() == SortBy && SortDir == "asc" ? "desc" : "asc";
}
