using Refit;

namespace RAMQ.Samples.Queue.TDF.SeqCon.Consumer.Http;

/// <summary>
/// Client HTTP vers le backend HOA5 — InscrireSuiviFichCorln (Étape 3 TDF).
/// </summary>
[Headers("Content-Type: application/json")]
public interface IHoa5BackendApi
{
    [Post("/hoa5/inscrire-suivi-fich")]
    Task<InscrireSuiviFichCorlnResponse> InscrireSuiviFichCorlnAsync(
        [Header("Idempotency-Key")] string idempotencyKey,
        [Header("X-Correlation-Id")] string correlationId,
        [Body] InscrireSuiviFichCorlnRequest request,
        CancellationToken cancellationToken = default);
}

public record InscrireSuiviFichCorlnRequest
{
    public required string NoEchg          { get; init; }
    public required string AccuseReception { get; init; }
    public required string Erreur          { get; init; }
}

public record InscrireSuiviFichCorlnResponse
{
    public required string IndExecOrcSpec { get; init; }
    public required string NoEchg         { get; init; }
    public required string Erreur         { get; init; }
}
