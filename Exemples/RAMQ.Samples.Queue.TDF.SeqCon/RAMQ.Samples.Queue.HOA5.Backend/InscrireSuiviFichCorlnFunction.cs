using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RAMQ.Samples.Queue.TDF.SeqCon.Consumer.Http;
using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace RAMQ.Samples.Queue.HOA5.Backend;

/// <summary>
/// HOA5 Backend — Simulation du service HOA5 / inscrire-suivi-fich.
///
/// ════════════════════════════════════════════════════════════════════════
/// Rôle dans le pattern Sequential Convoy
/// ════════════════════════════════════════════════════════════════════════
///
/// Ce service simule l'API backend HOA5 qui reçoit la corrélation du TDF.
/// Il est appelé dans deux contextes :
///
///   1. Par TdfSeqConConsumer (step tdf.correller) via IHoa5BackendApi (Refit)
///      → Depuis le Subscriber, lors du traitement du message de corrélation.
///
///   2. Par HOA5.Consumer
///      → Depuis la file de résultats, après que l'orchestrateur a publié
///        le TransactionCorrelationResult.
///
/// Contrat HTTP :
///   POST /hoa5/inscrire-suivi-fich
///   Header: Idempotency-Key   — clé d'idempotence pour éviter les doublons
///   Header: X-Correlation-Id  — identifiant de corrélation de bout en bout
///   Body:   InscrireSuiviFichCorlnRequest { NoEchg, AccuseReception, Erreur }
///
/// Réponse simulée :
///   InscrireSuiviFichCorlnResponse { IndExecOrcSpec="01", NoEchg, Erreur="0" }
///   IndExecOrcSpec="01" = Exécution réussie.
///
/// Observabilité DFO :
///   BeginScope → IdempotencyKey, CorrelationId, NoEchg propagés dans chaque log.
///   Stopwatch → Duration publiée pour alertes SLA.
/// </summary>
public sealed class InscrireSuiviFichCorlnFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<InscrireSuiviFichCorlnFunction> _logger;

    public InscrireSuiviFichCorlnFunction(ILogger<InscrireSuiviFichCorlnFunction> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [Function("InscrireSuiviFichCorln")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "hoa5/inscrire-suivi-fich")]
        HttpRequestData req,
        CancellationToken ct)
    {
        var sw             = Stopwatch.StartNew();
        var idempotencyKey = req.Headers.TryGetValues("Idempotency-Key", out var idkValues)
            ? string.Join(",", idkValues) : string.Empty;
        var correlationId  = req.Headers.TryGetValues("X-Correlation-Id", out var cidValues)
            ? string.Join(",", cidValues) : string.Empty;

        InscrireSuiviFichCorlnRequest? request;
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync(ct);
            request  = JsonSerializer.Deserialize<InscrireSuiviFichCorlnRequest>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Corps de la requête invalide. IdempotencyKey={IdempotencyKey}",
                idempotencyKey);
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Corps JSON invalide.", ct);
            return badRequest;
        }

        if (request is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Requête vide.", ct);
            return badRequest;
        }

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["IdempotencyKey"] = idempotencyKey,
            ["CorrelationId"]  = correlationId,
            ["NoEchg"]         = request.NoEchg
        });

        _logger.LogInformation(
            "HOA5 inscrire-suivi-fich reçu. NoEchg={NoEchg}, AccuseReception={AccuseReception}",
            request.NoEchg, request.AccuseReception);

        // Simulation : réponse de succès
        var response = new InscrireSuiviFichCorlnResponse
        {
            IndExecOrcSpec = "01",   // 01 = exécution réussie
            NoEchg         = request.NoEchg,
            Erreur         = "0"
        };

        _logger.LogInformation(
            "HOA5 inscrire-suivi-fich terminé. NoEchg={NoEchg}, Duration={Duration}ms",
            request.NoEchg, sw.ElapsedMilliseconds);

        var ok = req.CreateResponse(HttpStatusCode.OK);
        ok.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await ok.WriteStringAsync(JsonSerializer.Serialize(response, JsonOptions), ct);
        return ok;
    }
}
