using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip;
using RAMQ.Samples.Queue.RoutingSlip.Message;
using System.Net;
using System.Text.Json;

namespace RAMQ.Samples.Queue.RoutingSlip.Activateur
{
    /// <summary>
    /// Azure Function HTTP — Point de départ du workflow Routing Slip.
    ///
    /// Responsabilités :
    ///   1. Valider la requête HTTP entrante.
    ///   2. Construire un SlipEnvelope avec RoutingSlipBuilder.
    ///   3. Publier le slip vers la première étape (ValiderAdmissibilite).
    ///
    /// L'activateur ne connaît pas la logique métier — il délègue entièrement au slip.
    /// </summary>
    public class DossierActivateur
    {
        private readonly ILogger<DossierActivateur> _logger;
        private readonly RoutingSlipBuilder _builder;
        private readonly IMessageProducer<SlipEnvelope> _producer;

        public DossierActivateur(
            ILogger<DossierActivateur> logger,
            RoutingSlipBuilder builder,
            IMessageProducer<SlipEnvelope> producer)
        {
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
            _builder  = builder  ?? throw new ArgumentNullException(nameof(builder));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        [Function(nameof(DossierActivateur))]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "dossiers")] HttpRequestData req,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("Activateur RoutingSlip — réception d'une demande de traitement.");

            // 1. Lire et valider le corps de la requête
            DemandeTraitementRequest? demande;
            try
            {
                demande = await JsonSerializer.DeserializeAsync<DemandeTraitementRequest>(
                    req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                    cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Corps de la requête invalide.");
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("Corps JSON invalide.", cancellationToken);
                return badRequest;
            }

            if (demande == null || string.IsNullOrWhiteSpace(demande.DossierId))
            {
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteStringAsync("DossierId est requis.", cancellationToken);
                return badRequest;
            }

            // 2. Construire le slip
            // RÈGLE : stepName == Target dans AppSettings.Endpoints du worker
            var slip = _builder
                .AddStep("ValiderAdmissibilite", new ValiderAdmissibiliteArgs
                {
                    DossierId    = demande.DossierId,
                    NumeroNam    = demande.NumeroNam ?? string.Empty,
                    DateDemande  = DateOnly.FromDateTime(DateTime.UtcNow)
                })
                .AddStep("EnrichirDonnees", new EnrichirDonneesArgs
                {
                    DossierId = demande.DossierId,
                    Source    = "ReferentielBeneficiaires"
                })
                .AddStep("NotifierBeneficiaire", new NotifierBeneficiaireArgs
                {
                    DossierId = demande.DossierId,
                    Canal     = demande.CanalNotification ?? "email"
                })
                .Build();

            // 3. Publier vers la première étape
            var context = new MessageTransitContext<SlipEnvelope>
            {
                MessageId     = slip.Header.SlipId,
                CorrelationId = slip.Header.SlipId,
                Message       = slip
            };

            _logger.LogInformation(
                "Publication du slip {SlipId} ({SlipName}) — {StepCount} étapes.",
                slip.Header.SlipId, slip.Header.SlipName, slip.Steps.Length);

            await _producer.PublishAsync(context, cancellationToken: cancellationToken);

            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(new { slipId = slip.Header.SlipId }, cancellationToken);
            return response;
        }
    }

    /// <summary>Corps de la requête HTTP POST /dossiers.</summary>
    public sealed class DemandeTraitementRequest
    {
        public string DossierId { get; set; } = default!;
        public string? NumeroNam { get; set; }
        public string? CanalNotification { get; set; }
    }
}
