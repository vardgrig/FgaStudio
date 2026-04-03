using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.AuthModels;

public class DetailsModel : PageModel
{
    private readonly ConnectionManager _connectionManager;

    [BindProperty(SupportsGet = true)]
    public string? StoreId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ModelId { get; set; }

    public string? ActiveConnectionName { get; private set; }
    public string? ActiveStoreId { get; private set; }
    public AuthorizationModelDetailViewModel? Detail { get; private set; }
    public string? Error { get; private set; }
    public bool IsDbMode { get; private set; }

    public DetailsModel(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task OnGetAsync()
    {
        var active = _connectionManager.GetActive();
        ActiveConnectionName = active?.Name;
        ActiveStoreId = active?.StoreId;
        IsDbMode = active?.Type == ConnectionType.Database;

        if (active is null || string.IsNullOrEmpty(StoreId) || string.IsNullOrEmpty(ModelId))
            return;

        var service = _connectionManager.BuildService(active);
        if (service is null) return;

        try
        {
            Detail = await service.GetAuthorizationModelAsync(StoreId, ModelId);
        }
        catch (Exception ex)
        {
            Error = $"Failed to load authorization model: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnGetDownloadAsync()
    {
        if (string.IsNullOrEmpty(StoreId) || string.IsNullOrEmpty(ModelId))
            return BadRequest();

        var active = _connectionManager.GetActive();
        if (active is null) return BadRequest();

        var service = _connectionManager.BuildService(active);
        if (service is null) return BadRequest();

        var detail = await service.GetAuthorizationModelAsync(StoreId, ModelId);
        if (detail?.SchemaJson is null) return NotFound();

        var bytes = System.Text.Encoding.UTF8.GetBytes(detail.SchemaJson);
        return File(bytes, "application/json", $"auth-model-{ModelId[..Math.Min(8, ModelId.Length)]}.json");
    }
}
