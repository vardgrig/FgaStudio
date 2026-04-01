using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.Connection;

public class IndexModel : PageModel
{
    private readonly ConnectionManager _connectionManager;

    public IReadOnlyList<ConnectionConfig> Connections { get; private set; } = [];
    public string? ActiveConnectionName { get; private set; }
    public string? ActiveStoreId { get; private set; }

    public IndexModel(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public void OnGet()
    {
        Load();
    }

    public async Task<IActionResult> OnPostSetActiveAsync(string name)
    {
        await _connectionManager.SetActiveConnectionAsync(name);
        TempData["Success"] = $"'{name}' is now the active connection.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(string name)
    {
        await _connectionManager.DeleteConnectionAsync(name);
        TempData["Success"] = $"Connection '{name}' deleted.";
        return RedirectToPage();
    }

    private void Load()
    {
        Connections = _connectionManager.GetAll();
        var active = _connectionManager.GetActive();
        ActiveConnectionName = active?.Name;
        ActiveStoreId = active?.StoreId;
    }
}
