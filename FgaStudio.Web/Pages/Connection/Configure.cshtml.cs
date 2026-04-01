using System.ComponentModel.DataAnnotations;
using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.Connection;

public class ConfigureModel : PageModel
{
    private readonly ConnectionManager _connectionManager;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool IsEdit { get; private set; }
    public bool? TestResult { get; private set; }
    public string? ActiveConnectionName => _connectionManager.GetActive()?.Name;

    public ConfigureModel(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public void OnGet(string? name)
    {
        if (name is not null)
        {
            var existing = _connectionManager.GetByName(name);
            if (existing is not null)
            {
                IsEdit = true;
                Input = new InputModel
                {
                    Name = existing.Name,
                    Type = existing.Type,
                    ApiUrl = existing.ApiUrl,
                    ApiToken = existing.ApiToken,
                    ConnectionString = existing.ConnectionString
                };
            }
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidateInput();
        if (!ModelState.IsValid) return Page();

        var config = BuildConfig();
        await _connectionManager.SaveConnectionAsync(config);

        TempData["Success"] = $"Connection '{config.Name}' saved successfully.";
        return RedirectToPage("/Connection/Index");
    }

    public async Task<IActionResult> OnPostTestAsync()
    {
        ValidateInput();
        if (!ModelState.IsValid) return Page();

        var config = BuildConfig();
        var service = _connectionManager.BuildService(config);
        TestResult = service is not null && await service.TestConnectionAsync();
        IsEdit = _connectionManager.GetByName(Input.Name) is not null;
        return Page();
    }

    private ConnectionConfig BuildConfig() => new()
    {
        Name = Input.Name,
        Type = Input.Type,
        ApiUrl = Input.ApiUrl,
        ApiToken = Input.ApiToken,
        ConnectionString = Input.ConnectionString,
        // Preserve existing store context if editing
        StoreId = _connectionManager.GetByName(Input.Name)?.StoreId,
        AuthorizationModelId = _connectionManager.GetByName(Input.Name)?.AuthorizationModelId
    };

    private void ValidateInput()
    {
        if (Input.Type == ConnectionType.Url && string.IsNullOrWhiteSpace(Input.ApiUrl))
            ModelState.AddModelError("Input.ApiUrl", "API URL is required for URL connections.");

        if (Input.Type == ConnectionType.Database && string.IsNullOrWhiteSpace(Input.ConnectionString))
            ModelState.AddModelError("Input.ConnectionString", "Connection string is required for database connections.");
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Connection name is required.")]
        public string Name { get; set; } = string.Empty;
        public ConnectionType Type { get; set; } = ConnectionType.Url;
        public string? ApiUrl { get; set; }
        public string? ApiToken { get; set; }
        public string? ConnectionString { get; set; }
    }
}
