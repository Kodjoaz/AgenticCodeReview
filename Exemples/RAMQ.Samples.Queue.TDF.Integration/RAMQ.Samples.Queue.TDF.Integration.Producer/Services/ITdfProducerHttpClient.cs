using Refit;

namespace RAMQ.Samples.Queue.TDF.Integration.Producer.Services;

/// <summary>
/// HTTP client interface for Frontend to communicate with Producer via REST.
/// Uses Refit for type-safe HTTP communication.
/// </summary>
[Headers("Content-Type: application/json")]
public interface ITdfProducerHttpClient
{
    /// <summary>
    /// POST /api/tdf/transaction/initial
    /// Frontend sends initial TDF transaction request to Producer.
    /// </summary>
    [Post("/api/tdf/transaction/initial")]
    Task<TdfPublishResult> PublishInitialTransactionAsync(
        [Body] TdfTransactionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// POST /api/tdf/transaction/correlation
    /// Frontend sends correlation confirmation to Producer.
    /// </summary>
    [Post("/api/tdf/transaction/correlation")]
    Task<TdfPublishResult> PublishCorrelationAsync(
        [Body] TdfCorrelationRequest request,
        CancellationToken cancellationToken = default);
}
