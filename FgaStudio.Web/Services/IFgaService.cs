using FgaStudio.Web.Models;

namespace FgaStudio.Web.Services;

public interface IFgaService
{
    Task<int> CountTuplesAsync(string storeId, TupleFilter filter);
    Task<List<StoreViewModel>> GetStoresAsync();
    Task<List<AuthorizationModelViewModel>> GetAuthorizationModelsAsync(string storeId);
    Task<(List<TupleViewModel> Tuples, string? ContinuationToken)> ReadTuplesAsync(
        string storeId, string modelId, TupleFilter filter, string? continuationToken = null);
    Task WriteTupleAsync(string storeId, string modelId, TupleKey tuple);
    Task DeleteTupleAsync(string storeId, string modelId, TupleKey tuple);
    Task<AuthorizationModelDetailViewModel?> GetAuthorizationModelAsync(string storeId, string modelId);
    Task<(bool Success, string? Error)> TestConnectionAsync();
}
