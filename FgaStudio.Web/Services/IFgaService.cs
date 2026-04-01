using FgaStudio.Web.Models;

namespace FgaStudio.Web.Services;

public interface IFgaService
{
    Task<List<StoreViewModel>> GetStoresAsync();
    Task<List<AuthorizationModelViewModel>> GetAuthorizationModelsAsync(string storeId);
    Task<(List<TupleViewModel> Tuples, string? ContinuationToken)> ReadTuplesAsync(
        string storeId, string modelId, TupleFilter filter, string? continuationToken = null);
    Task WriteTupleAsync(string storeId, string modelId, TupleKey tuple);
    Task DeleteTupleAsync(string storeId, string modelId, TupleKey tuple);
    Task<bool> TestConnectionAsync();
}
