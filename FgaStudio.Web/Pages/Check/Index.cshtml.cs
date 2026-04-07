using FgaStudio.Web.Models;
using FgaStudio.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FgaStudio.Web.Pages.Check;

public class IndexModel : PageModel
{
    private readonly ConnectionManager _connectionManager;

    public ConnectionConfig? ActiveConnection    { get; private set; }
    public string? ActiveConnectionName          { get; private set; }
    public string? ActiveStoreId                 { get; private set; }
    public string? ActiveModelId                 { get; private set; }
    public bool IsReady                          { get; private set; }
    public bool IsDbMode                         { get; private set; }

    // ── Inputs (GET-bound so results are bookmarkable) ────────────────────────
    [BindProperty(SupportsGet = true)] public string  Mode     { get; set; } = "check";
    [BindProperty(SupportsGet = true)] public string? User     { get; set; }
    [BindProperty(SupportsGet = true)] public string? Relation { get; set; }
    [BindProperty(SupportsGet = true)] public string? Object   { get; set; }
    [BindProperty(SupportsGet = true)] public string? Type     { get; set; }
    [BindProperty(SupportsGet = true)] public string? UserType { get; set; }

    // ── Results ───────────────────────────────────────────────────────────────
    public bool         HasResult  { get; private set; }
    public bool?        Allowed    { get; private set; }
    public List<string> ResultList { get; private set; } = [];
    public string?      TreeJson   { get; private set; }
    public string?      Error      { get; private set; }

    public IndexModel(ConnectionManager connectionManager)
    {
        _connectionManager = connectionManager;
    }

    public async Task OnGetAsync()
    {
        ActiveConnection    = _connectionManager.GetActive();
        ActiveConnectionName = ActiveConnection?.Name;
        ActiveStoreId       = ActiveConnection?.StoreId;
        ActiveModelId       = ActiveConnection?.AuthorizationModelId;
        IsReady             = ActiveConnection is not null && !string.IsNullOrEmpty(ActiveStoreId);
        IsDbMode            = ActiveConnection?.Type == ConnectionType.Database;

        if (!IsReady || !HasRequiredInputs()) return;

        HasResult = true;
        var service = _connectionManager.BuildService(ActiveConnection!);
        if (service is null) { Error = "Failed to build service."; return; }

        try
        {
            switch (Mode)
            {
                case "check":
                    (Allowed, Error) = await service.CheckAsync(
                        ActiveStoreId!, ActiveModelId ?? "",
                        new TupleKey { User = User!, Relation = Relation!, Object = Object! });
                    break;

                case "expand":
                    (TreeJson, Error) = await service.ExpandAsync(
                        ActiveStoreId!, ActiveModelId ?? "", Relation!, Object!);
                    break;

                case "list-objects":
                    (ResultList, Error) = await service.ListObjectsAsync(
                        ActiveStoreId!, ActiveModelId ?? "", User!, Relation!, Type!);
                    break;

                case "list-users":
                    (ResultList, Error) = await service.ListUsersAsync(
                        ActiveStoreId!, ActiveModelId ?? "", Object!, Relation!,
                        string.IsNullOrWhiteSpace(UserType) ? "user" : UserType);
                    break;
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private bool HasRequiredInputs() => Mode switch
    {
        "check"        => !string.IsNullOrWhiteSpace(User)
                          && !string.IsNullOrWhiteSpace(Relation)
                          && !string.IsNullOrWhiteSpace(Object),
        "expand"       => !string.IsNullOrWhiteSpace(Relation)
                          && !string.IsNullOrWhiteSpace(Object),
        "list-objects" => !string.IsNullOrWhiteSpace(User)
                          && !string.IsNullOrWhiteSpace(Relation)
                          && !string.IsNullOrWhiteSpace(Type),
        "list-users"   => !string.IsNullOrWhiteSpace(Object)
                          && !string.IsNullOrWhiteSpace(Relation),
        _              => false
    };
}
