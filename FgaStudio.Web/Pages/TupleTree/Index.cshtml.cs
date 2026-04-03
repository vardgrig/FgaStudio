using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.TupleTree;

// Represents one entity in the left panel, knowing which filter param to use when clicked.
public record EntityItem(string Value, bool IsObjectFilter);

public class IndexModel : PageModel
{
    private readonly ConnectionManager _connectionManager;
    private readonly TupleCacheService _tupleCache;

    private const int TreeCap = 500;

    public ConnectionConfig? ActiveConnection { get; private set; }
    public string? ActiveConnectionName { get; private set; }
    public string? ActiveStoreId { get; private set; }
    public string? ActiveModelId { get; private set; }
    public bool IsReady { get; private set; }
    public bool IsUrlConnection { get; private set; }
    public bool CacheIsEmpty { get; private set; }
    public DateTime? CachedAt { get; private set; }

    // Object perspective: ?object=xxx  →  Object → Relation → Users
    [BindProperty(Name = "object", SupportsGet = true)]
    public string? ObjectFilter { get; set; }

    // User perspective: ?user=xxx  →  User → Relation → Objects
    [BindProperty(Name = "user", SupportsGet = true)]
    public string? UserFilter { get; set; }

    public bool IsUserPerspective => !string.IsNullOrEmpty(UserFilter);
    public string? ActiveFilter => IsUserPerspective ? UserFilter : ObjectFilter;

    // Reuses TupleObjectNode for both perspectives:
    //   Object perspective → .Object = object value, .Relations[].Users = user values
    //   User perspective   → .Object = user value,   .Relations[].Users = object values
    public List<TupleObjectNode> Tree { get; private set; } = [];

    // Left panel: type-prefix → sorted list of entities (objects + users combined)
    public Dictionary<string, List<EntityItem>> EntityGroups { get; private set; } = [];

    public int TotalCount { get; private set; }
    public bool IsTruncated { get; private set; }
    public string? Error { get; private set; }

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
        ActiveModelId = ActiveConnection?.AuthorizationModelId;

        IsReady = ActiveConnection is not null && !string.IsNullOrEmpty(ActiveStoreId);
        if (!IsReady) return;

        IsUrlConnection = ActiveConnection!.Type == ConnectionType.Url;

        var service = _connectionManager.BuildService(ActiveConnection!);
        if (service is null) return;

        // Build the filter: filter by User column when in user perspective, Object column otherwise.
        var filter = new TupleFilter
        {
            Object   = IsUserPerspective ? null : ObjectFilter,
            User     = IsUserPerspective ? UserFilter : null,
            Page     = 1,
            PageSize = TreeCap,
        };

        // For the entity browser we always load the full unfiltered set (capped) so the
        // left panel stays populated regardless of which entity is selected.
        var browserFilter = new TupleFilter { Page = 1, PageSize = TreeCap };

        try
        {
            List<TupleViewModel> filteredTuples;
            List<TupleViewModel> allTuples;

            if (IsUrlConnection)
            {
                var meta = await _tupleCache.GetMetaAsync(ActiveConnectionName!, ActiveStoreId!);
                if (meta is null) { CacheIsEmpty = true; return; }

                CachedAt = meta.CachedAt;

                // Run both queries in parallel.
                var filteredTask = _tupleCache.QueryAsync(ActiveConnectionName!, ActiveStoreId!, filter);
                var allTask      = (filter.User != null || filter.Object != null)
                    ? _tupleCache.QueryAsync(ActiveConnectionName!, ActiveStoreId!, browserFilter)
                    : Task.FromResult<CachedTupleResult>(null!);

                await Task.WhenAll(filteredTask, allTask);

                var filteredResult = filteredTask.Result;
                TotalCount = filteredResult.TotalCount;
                filteredTuples = filteredResult.Tuples;
                allTuples = (allTask.Result is { } r) ? r.Tuples : filteredTuples;
            }
            else
            {
                var countTask    = service.CountTuplesAsync(ActiveStoreId!, filter);
                var dataTask     = service.ReadTuplesAsync(ActiveStoreId!, ActiveModelId ?? string.Empty, filter);
                var browserTask  = (filter.User != null || filter.Object != null)
                    ? service.ReadTuplesAsync(ActiveStoreId!, ActiveModelId ?? string.Empty, browserFilter)
                    : Task.FromResult<(List<TupleViewModel>, string?)>((null!, null));

                await Task.WhenAll(countTask, dataTask, browserTask);

                TotalCount = countTask.Result;
                filteredTuples = dataTask.Result.Tuples;
                allTuples = browserTask.Result.Item1 ?? filteredTuples;
            }

            IsTruncated = TotalCount > TreeCap;
            Tree = IsUserPerspective ? BuildUserTree(filteredTuples) : BuildObjectTree(filteredTuples);
            EntityGroups = BuildEntityGroups(allTuples);
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
            TempData["Success"] = "Tuple cache refreshed.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Cache refresh failed: {ex.Message}";
        }

        return RedirectToPage();
    }

    // ─── Tree builders ───────────────────────────────────────────────────────────

    // Object perspective: Object → Relation → Users
    private static List<TupleObjectNode> BuildObjectTree(List<TupleViewModel> tuples) =>
        tuples
            .GroupBy(t => t.Object)
            .OrderBy(g => g.Key)
            .Select(g => new TupleObjectNode
            {
                Object = g.Key,
                Relations = g
                    .GroupBy(t => t.Relation)
                    .OrderBy(rg => rg.Key)
                    .Select(rg => new TupleRelationGroup
                    {
                        Relation = rg.Key,
                        Users = rg.Select(t => t.User).OrderBy(u => u).ToList()
                    })
                    .ToList()
            })
            .ToList();

    // User perspective: User → Relation → Objects
    // Reuses TupleObjectNode: .Object = the user entity, .Relations[].Users = the objects
    private static List<TupleObjectNode> BuildUserTree(List<TupleViewModel> tuples) =>
        tuples
            .GroupBy(t => t.User)
            .OrderBy(g => g.Key)
            .Select(g => new TupleObjectNode
            {
                Object = g.Key,
                Relations = g
                    .GroupBy(t => t.Relation)
                    .OrderBy(rg => rg.Key)
                    .Select(rg => new TupleRelationGroup
                    {
                        Relation = rg.Key,
                        Users = rg.Select(t => t.Object).OrderBy(o => o).ToList()
                    })
                    .ToList()
            })
            .ToList();

    // ─── Entity browser ──────────────────────────────────────────────────────────

    // Combines unique Objects and Users from all loaded tuples.
    // Objects take priority: if a value appears in both columns it links to ?object=
    private static Dictionary<string, List<EntityItem>> BuildEntityGroups(List<TupleViewModel> tuples)
    {
        var objectSet = tuples.Select(t => t.Object).ToHashSet(StringComparer.Ordinal);

        return tuples
            .SelectMany(t => new[]
            {
                (Value: t.Object, IsObj: true),
                (Value: t.User,   IsObj: false),
            })
            .GroupBy(e => e.Value)
            .Select(g => new EntityItem(
                Value: g.Key,
                IsObjectFilter: g.Any(e => e.IsObj)))   // prefer object link when it appears as an object
            .GroupBy(e => e.Value.Contains(':') ? e.Value[..e.Value.IndexOf(':')] : e.Value)
            .OrderBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(e => e.Value).ToList());
    }
}
