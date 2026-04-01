using FgaStudio.Web.Configuration;
using FgaStudio.Web.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace FgaStudio.Web.Services;

public class ConnectionManager
{
    private readonly IWebHostEnvironment _env;
    private FgaStudioSettings _settings;

    public ConnectionManager(IOptionsMonitor<FgaStudioSettings> options, IWebHostEnvironment env)
    {
        _env = env;
        _settings = options.CurrentValue;
        options.OnChange(s => _settings = s);
    }

    public IReadOnlyList<ConnectionConfig> GetAll() => _settings.Connections.AsReadOnly();

    public ConnectionConfig? GetActive()
    {
        if (_settings.ActiveConnectionName is null) return null;
        return _settings.Connections.FirstOrDefault(c => c.Name == _settings.ActiveConnectionName);
    }

    public ConnectionConfig? GetByName(string name) =>
        _settings.Connections.FirstOrDefault(c => c.Name == name);

    public IFgaService? BuildService(ConnectionConfig config) => config.Type switch
    {
        ConnectionType.Url => new FgaHttpService(config),
        ConnectionType.Database => new FgaDbService(config),
        _ => null
    };

    public IFgaService? BuildActiveService()
    {
        var active = GetActive();
        return active is null ? null : BuildService(active);
    }

    public async Task SaveConnectionAsync(ConnectionConfig connection)
    {
        var existing = _settings.Connections.FindIndex(c => c.Name == connection.Name);
        if (existing >= 0)
            _settings.Connections[existing] = connection;
        else
            _settings.Connections.Add(connection);

        await PersistAsync();
    }

    public async Task DeleteConnectionAsync(string name)
    {
        _settings.Connections.RemoveAll(c => c.Name == name);
        if (_settings.ActiveConnectionName == name)
            _settings.ActiveConnectionName = _settings.Connections.FirstOrDefault()?.Name;

        await PersistAsync();
    }

    public async Task SetActiveConnectionAsync(string name)
    {
        _settings.ActiveConnectionName = name;
        await PersistAsync();
    }

    public async Task UpdateStoreContextAsync(string connectionName, string storeId, string modelId)
    {
        var conn = _settings.Connections.FirstOrDefault(c => c.Name == connectionName);
        if (conn is null) return;
        conn.StoreId = storeId;
        conn.AuthorizationModelId = modelId;
        await PersistAsync();
    }

    private async Task PersistAsync()
    {
        var appSettingsPath = Path.Combine(_env.ContentRootPath, "appsettings.json");

        Dictionary<string, JsonElement> root;
        if (File.Exists(appSettingsPath))
        {
            var existing = await File.ReadAllTextAsync(appSettingsPath);
            root = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(existing) ?? [];
        }
        else
        {
            root = [];
        }

        // Serialize the FgaStudio section and merge it in
        var settingsJson = JsonSerializer.SerializeToElement(_settings, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        root[FgaStudioSettings.Section] = settingsJson;

        var output = JsonSerializer.Serialize(root, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(appSettingsPath, output);
    }
}
