using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.Stores;

public class IndexModel : PageModel
{
    private readonly ConnectionManager _connectionManager;

    public ConnectionConfig? ActiveConnection { get; private set; }
    public string? ActiveConnectionName { get; private set; }
    public string? ActiveStoreId { get; private set; }
    public string? ActiveModelId { get; private set; }
    public List<StoreViewModel> Stores { get; private set; } = [];
    public List<AuthorizationModelViewModel> AuthorizationModels { get; private set; } = [];
    public string? Error { get; private set; }

    public IndexModel(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostSelectStoreAsync(string storeId)
    {
        var active = _connectionManager.GetActive();
        if (active is null) return RedirectToPage();

        // Clear model when switching stores
        await _connectionManager.UpdateStoreContextAsync(active.Name, storeId, string.Empty);
        TempData["Success"] = "Store selected.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSelectModelAsync(string modelId)
    {
        var active = _connectionManager.GetActive();
        if (active is null || string.IsNullOrEmpty(active.StoreId)) return RedirectToPage();

        await _connectionManager.UpdateStoreContextAsync(active.Name, active.StoreId, modelId);
        TempData["Success"] = "Authorization model selected.";
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        ActiveConnection = _connectionManager.GetActive();
        ActiveConnectionName = ActiveConnection?.Name;
        ActiveStoreId = ActiveConnection?.StoreId;
        ActiveModelId = ActiveConnection?.AuthorizationModelId;

        if (ActiveConnection is null) return;

        var service = _connectionManager.BuildService(ActiveConnection);
        if (service is null) return;

        try
        {
            Stores = await service.GetStoresAsync();

            if (!string.IsNullOrEmpty(ActiveStoreId))
                AuthorizationModels = await service.GetAuthorizationModelsAsync(ActiveStoreId);
        }
        catch (Exception ex)
        {
            Error = $"Failed to load stores: {ex.Message}";
        }
    }
}
