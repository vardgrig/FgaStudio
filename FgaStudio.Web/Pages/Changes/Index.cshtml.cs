using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.Changes;

public class IndexModel : PageModel
{
    private readonly ConnectionManager _connectionManager;

    public ConnectionConfig? ActiveConnection    { get; private set; }
    public string? ActiveConnectionName          { get; private set; }
    public string? ActiveStoreId                 { get; private set; }
    public bool IsReady                          { get; private set; }

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public string? TypeFilter { get; set; }

    [Microsoft.AspNetCore.Mvc.BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public List<TupleChangeViewModel> Changes { get; private set; } = [];
    public string? NextToken                  { get; private set; }
    public string? Error                      { get; private set; }

    public IndexModel(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task OnGetAsync()
    {
        ActiveConnection     = _connectionManager.GetActive();
        ActiveConnectionName = ActiveConnection?.Name;
        ActiveStoreId        = ActiveConnection?.StoreId;
        IsReady              = ActiveConnection is not null && !string.IsNullOrEmpty(ActiveStoreId);

        if (!IsReady) return;

        var service = _connectionManager.BuildService(ActiveConnection!);
        if (service is null) return;

        try
        {
            (Changes, NextToken) = await service.ReadChangesAsync(
                ActiveStoreId!, TypeFilter, pageSize: 50, continuationToken: Token);
        }
        catch (Exception ex)
        {
            Error = $"Failed to load changes: {ex.Message}";
        }
    }
}
