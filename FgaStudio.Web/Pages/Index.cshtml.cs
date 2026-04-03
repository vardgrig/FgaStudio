using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ConnectionManager _connectionManager;
    private readonly TupleCacheService _tupleCache;

    public ConnectionConfig? ActiveConnection { get; private set; }
    public string? ActiveConnectionName { get; private set; }
    public string? ActiveStoreId { get; private set; }

    public StoreViewModel? StoreDetail { get; private set; }
    public AuthorizationModelViewModel? ModelDetail { get; private set; }

    // Tuple count: -1 = not available, -2 = loading error
    public int TupleCount { get; private set; } = -1;
    public DateTime? CachedAt { get; private set; }

    public string? LoadError { get; private set; }

    public IndexModel(ConnectionManager connectionManager, TupleCacheService tupleCache)
    {
        _connectionManager = connectionManager;
        _tupleCache = tupleCache;
    }

    public async Task OnGetAsync()
    {
        ActiveConnection = _connectionManager.GetActive();
        ActiveConnectionName = ActiveConnection?.Name;
        ActiveStoreId = ActiveConnection?.StoreId;

        if (ActiveConnection is null) return;

        var service = _connectionManager.BuildService(ActiveConnection);
        if (service is null) return;

        try
        {
            // Always load stores to find the active one's details.
            var storesTask = service.GetStoresAsync();

            // Load auth model list if store is selected.
            Task<List<AuthorizationModelViewModel>>? modelsTask = null;
            if (!string.IsNullOrEmpty(ActiveConnection.StoreId))
                modelsTask = service.GetAuthorizationModelsAsync(ActiveConnection.StoreId);

            // Load tuple count.
            Task<int>? countTask = null;
            if (!string.IsNullOrEmpty(ActiveConnection.StoreId))
            {
                if (ActiveConnection.Type == ConnectionType.Url)
                {
                    // For HTTP, read from cache meta (instant, no API call).
                    var metaTask = _tupleCache.GetMetaAsync(ActiveConnection.Name, ActiveConnection.StoreId);
                    await Task.WhenAll(storesTask, modelsTask ?? Task.CompletedTask, metaTask);
                    var meta = metaTask.Result;
                    if (meta is not null)
                    {
                        TupleCount = meta.TotalTuples;
                        CachedAt = meta.CachedAt;
                    }
                }
                else
                {
                    countTask = service.CountTuplesAsync(ActiveConnection.StoreId, new TupleFilter());
                    await Task.WhenAll(storesTask, modelsTask ?? Task.CompletedTask, countTask);
                    TupleCount = countTask.Result;
                }
            }
            else
            {
                await storesTask;
            }

            StoreDetail = storesTask.Result.FirstOrDefault(s => s.IsActive);

            if (modelsTask is not null)
                ModelDetail = modelsTask.Result.FirstOrDefault(m => m.IsActive);
        }
        catch (Exception ex)
        {
            LoadError = ex.Message;
        }
    }
}
