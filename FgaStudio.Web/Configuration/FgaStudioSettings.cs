using FgaStudio.Web.Models;

namespace FgaStudio.Web.Configuration;

public class FgaStudioSettings
{
    public const string Section = "FgaStudio";

    public List<ConnectionConfig> Connections { get; set; } = [];
    public string? ActiveConnectionName { get; set; }
}
