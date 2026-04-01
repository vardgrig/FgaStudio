using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.Tuples;

public class IndexModel : PageModel
{
    private readonly ConnectionManager _connectionManager;

    public ConnectionConfig? ActiveConnection { get; private set; }
    public string? ActiveConnectionName { get; private set; }
    public string? ActiveStoreId { get; private set; }
    public string? ActiveModelId { get; private set; }
    public bool IsReady { get; private set; }

    public TupleFilter Filter { get; private set; } = new();
    public List<TupleViewModel> Tuples { get; private set; } = [];
    public bool HasNextPage { get; private set; }
    public string? Error { get; private set; }

    public IndexModel(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task OnGetAsync(
        string? user, string? relation, string? @object,
        int page = 1, int pageSize = 25)
    {
        ActiveConnection = _connectionManager.GetActive();
        ActiveConnectionName = ActiveConnection?.Name;
        ActiveStoreId = ActiveConnection?.StoreId;
        ActiveModelId = ActiveConnection?.AuthorizationModelId;

        IsReady = ActiveConnection is not null && !string.IsNullOrEmpty(ActiveStoreId);
        if (!IsReady) return;

        Filter = new TupleFilter
        {
            User = user,
            Relation = relation,
            Object = @object,
            Page = Math.Max(1, page),
            PageSize = pageSize
        };

        var service = _connectionManager.BuildService(ActiveConnection!);
        if (service is null) return;

        try
        {
            // Fetch one extra to detect if there's a next page
            var fetchFilter = new TupleFilter
            {
                User = Filter.User,
                Relation = Filter.Relation,
                Object = Filter.Object,
                Page = Filter.Page,
                PageSize = Filter.PageSize + 1
            };

            var (tuples, _) = await service.ReadTuplesAsync(ActiveStoreId!, ActiveModelId ?? string.Empty, fetchFilter);
            HasNextPage = tuples.Count > Filter.PageSize;
            Tuples = tuples.Take(Filter.PageSize).ToList();
        }
        catch (Exception ex)
        {
            Error = $"Failed to load tuples: {ex.Message}";
        }
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
            TempData["Success"] = "Tuple deleted successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to delete tuple: {ex.Message}";
        }

        return RedirectToPage();
    }
}
