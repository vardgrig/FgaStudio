using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages;

public class IndexModel : PageModel
{
    private readonly ConnectionManager _connectionManager;

    public ConnectionConfig? ActiveConnection { get; private set; }
    public string? ActiveConnectionName { get; private set; }
    public string? ActiveStoreId { get; private set; }

    public IndexModel(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public void OnGet()
    {
        ActiveConnection = _connectionManager.GetActive();
        ActiveConnectionName = ActiveConnection?.Name;
        ActiveStoreId = ActiveConnection?.StoreId;
    }
}
