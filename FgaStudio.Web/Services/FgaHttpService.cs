using FgaStudio.Web.Models;
using AppTupleKey = FgaStudio.Web.Models.TupleKey;
using OpenFga.Sdk.Client;
using OpenFga.Sdk.Client.Model;
using OpenFga.Sdk.Configuration;
using OpenFga.Sdk.Model;

namespace FgaStudio.Web.Services;

public class FgaHttpService : IFgaService
{
    private readonly ConnectionConfig _config;

    public FgaHttpService(ConnectionConfig config)
    {
        _config = config;
    }

    private OpenFgaClient BuildClient(string? storeId = null, string? modelId = null)
    {
        var clientConfig = new ClientConfiguration
        {
            ApiUrl = _config.ApiUrl ?? "http://localhost:8080",
            StoreId = storeId,
            AuthorizationModelId = modelId,
        };

        switch (_config.AuthMethod)
        {
            case Models.AuthMethod.ApiToken when !string.IsNullOrWhiteSpace(_config.ApiToken):
                clientConfig.Credentials = new Credentials
                {
                    Method = CredentialsMethod.ApiToken,
                    Config = new CredentialsConfig { ApiToken = _config.ApiToken }
                };
                break;

            case Models.AuthMethod.ClientCredentials:
                clientConfig.Credentials = new Credentials
                {
                    Method = CredentialsMethod.ClientCredentials,
                    Config = new CredentialsConfig
                    {
                        ClientId = _config.ClientId,
                        ClientSecret = _config.ClientSecret,
                        ApiAudience = _config.ApiAudience,
                        ApiTokenIssuer = _config.ApiTokenIssuer
                    }
                };
                break;
        }

        return new OpenFgaClient(clientConfig);
    }

    // COUNT is not supported via the HTTP API — use TupleCacheService for HTTP connections.
    public Task<int> CountTuplesAsync(string storeId, TupleFilter filter) => Task.FromResult(-1);

    public async Task<(bool Success, string? Error)> TestConnectionAsync()
    {
        try
        {
            var client = BuildClient();
            await client.ListStores(null, null);
            return (true, null);
        }
        catch (Exception ex)
        {
            var root = ex.GetBaseException();
            var msg = root.Message == ex.Message
                ? $"{ex.GetType().Name}: {ex.Message}"
                : $"{ex.GetType().Name}: {ex.Message} → {root.GetType().Name}: {root.Message}";
            return (false, msg);
        }
    }

    public async Task<List<StoreViewModel>> GetStoresAsync()
    {
        var client = BuildClient();
        var response = await client.ListStores(null, null);
        return response.Stores?.Select(s => new StoreViewModel
        {
            Id = s.Id,
            Name = s.Name,
            CreatedAt = s.CreatedAt,
            UpdatedAt = s.UpdatedAt,
            IsActive = s.Id == _config.StoreId
        }).ToList() ?? [];
    }

    public async Task<List<AuthorizationModelViewModel>> GetAuthorizationModelsAsync(string storeId)
    {
        var client = BuildClient(storeId);
        var response = await client.ReadAuthorizationModels();
        return response.AuthorizationModels?.Select(m => new AuthorizationModelViewModel
        {
            Id = m.Id,
            SchemaVersion = m.SchemaVersion,
            TypeDefinitionCount = m.TypeDefinitions?.Count ?? 0,
            ConditionCount = m.Conditions?.Count ?? 0,
            IsActive = m.Id == _config.AuthorizationModelId
        }).ToList() ?? [];
    }

    public async Task<AuthorizationModelDetailViewModel?> GetAuthorizationModelAsync(string storeId, string modelId)
    {
        var client = BuildClient(storeId, modelId);
        var response = await client.ReadAuthorizationModel();
        var authModel = response.AuthorizationModel;
        if (authModel is null) return null;

        string? prettyJson = null;
        try
        {
            var rawJson = authModel.ToJson();
            using var doc = System.Text.Json.JsonDocument.Parse(rawJson);
            prettyJson = System.Text.Json.JsonSerializer.Serialize(doc,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }
        catch { /* leave null */ }

        var nodes = authModel.TypeDefinitions?.Select(td => new TypeDefinitionNode
        {
            Type = td.Type ?? "",
            Relations = td.Relations?.Keys.OrderBy(k => k).ToList() ?? []
        }).OrderBy(n => n.Type).ToList() ?? [];

        return new AuthorizationModelDetailViewModel
        {
            Id = authModel.Id ?? modelId,
            IsActive = authModel.Id == _config.AuthorizationModelId,
            SchemaJson = prettyJson,
            TypeDefinitions = nodes
        };
    }

    public async Task<(List<TupleViewModel> Tuples, string? ContinuationToken)> ReadTuplesAsync(
        string storeId, string modelId, TupleFilter filter, string? continuationToken = null)
    {
        var client = BuildClient(storeId, modelId);

        var body = new ClientReadRequest();
        if (!string.IsNullOrWhiteSpace(filter.User) ||
            !string.IsNullOrWhiteSpace(filter.Relation) ||
            !string.IsNullOrWhiteSpace(filter.Object))
        {
            body.User = filter.User;
            body.Relation = filter.Relation;
            body.Object = filter.Object;
        }

        var options = new ClientReadOptions
        {
            PageSize = filter.PageSize,
            ContinuationToken = continuationToken
        };

        var response = await client.Read(body, options);

        var tuples = response.Tuples?.Select(t => new TupleViewModel
        {
            User = t.Key?.User ?? "",
            Relation = t.Key?.Relation ?? "",
            Object = t.Key?.Object ?? "",
            Timestamp = t.Timestamp
        }).ToList() ?? [];

        return (tuples, response.ContinuationToken);
    }

    public async Task WriteTupleAsync(string storeId, string modelId, AppTupleKey tuple)
    {
        var client = BuildClient(storeId, modelId);
        await client.Write(new ClientWriteRequest
        {
            Writes =
            [
                new ClientTupleKey
                {
                    User = tuple.User,
                    Relation = tuple.Relation,
                    Object = tuple.Object
                }
            ]
        });
    }

    public async Task DeleteTupleAsync(string storeId, string modelId, AppTupleKey tuple)
    {
        var client = BuildClient(storeId, modelId);
        await client.Write(new ClientWriteRequest
        {
            Deletes =
            [
                new ClientTupleKeyWithoutCondition
                {
                    User = tuple.User,
                    Relation = tuple.Relation,
                    Object = tuple.Object
                }
            ]
        });
    }
}
