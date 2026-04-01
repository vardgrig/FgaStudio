using System.ComponentModel.DataAnnotations;
using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.Tuples;

public class WriteModel : PageModel
{
    private readonly ConnectionManager _connectionManager;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ActiveConnectionName { get; private set; }
    public string? ActiveStoreId { get; private set; }
    public string? ActiveModelId { get; private set; }

    public WriteModel(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public IActionResult OnGet()
    {
        var active = _connectionManager.GetActive();
        if (active is null || string.IsNullOrEmpty(active.StoreId))
        {
            TempData["Error"] = "Please select a store before writing tuples.";
            return RedirectToPage("/Tuples/Index");
        }

        ActiveConnectionName = active.Name;
        ActiveStoreId = active.StoreId;
        ActiveModelId = active.AuthorizationModelId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var active = _connectionManager.GetActive();
        if (active is null || string.IsNullOrEmpty(active.StoreId))
        {
            TempData["Error"] = "No active connection or store.";
            return RedirectToPage("/Tuples/Index");
        }

        ActiveConnectionName = active.Name;
        ActiveStoreId = active.StoreId;
        ActiveModelId = active.AuthorizationModelId;

        if (!ModelState.IsValid) return Page();

        var service = _connectionManager.BuildService(active);
        if (service is null) return Page();

        try
        {
            await service.WriteTupleAsync(active.StoreId, active.AuthorizationModelId ?? string.Empty,
                new TupleKey
                {
                    User = Input.User,
                    Relation = Input.Relation,
                    Object = Input.Object
                });

            TempData["Success"] = $"Tuple written: {Input.User} → {Input.Relation} → {Input.Object}";
            return RedirectToPage("/Tuples/Index");
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Failed to write tuple: {ex.Message}");
            return Page();
        }
    }

    public class InputModel
    {
        [Required(ErrorMessage = "User is required.")]
        public string User { get; set; } = string.Empty;

        [Required(ErrorMessage = "Relation is required.")]
        public string Relation { get; set; } = string.Empty;

        [Required(ErrorMessage = "Object is required.")]
        public string Object { get; set; } = string.Empty;
    }
}
