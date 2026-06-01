using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.Samples.Queue.MultiTarget.Message;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.MultiTarget.Worker
{
    /// <summary>
    /// BackgroundService qui publie des messages de réservation via IMultiTargetProducer.
    /// Tourne en round-robin : Car → Hotel → Flight → Car → … toutes les 30 secondes.
    ///
    /// Démonstration R17 :
    ///   - PublishAsync&lt;T&gt; : 1 message typé vers sa cible dédiée (zéro magic string)
    ///   - PublishMixedBatchAsync : les 3 types en un seul appel, chacun routé automatiquement
    /// </summary>
    public class DoWork : BackgroundService
    {
        private static long _iteration = -1;

        private readonly ILogger<DoWork> _logger;
        private readonly IMultiTargetProducer<IBookingMessage> _producer;

        public DoWork(
            ILogger<DoWork> logger,
            IMultiTargetProducer<IBookingMessage> producer)
        {
            _logger   = logger   ?? throw new ArgumentNullException(nameof(logger));
            _producer = producer ?? throw new ArgumentNullException(nameof(producer));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MultiTarget worker démarrant (R17 — IMultiTargetProducer)");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var iter = Interlocked.Increment(ref _iteration);

                    try
                    {
                        if (iter % 4 == 3)
                        {
                            // === Démo PublishMixedBatchAsync : 3 types en un seul appel ===
                            await PublierBatchHeterogeneAsync(stoppingToken);
                        }
                        else
                        {
                            // === Démo PublishAsync<T> : round-robin Car → Hotel → Flight ===
                            await PublierMessageTypéAsync((int)(iter % 3), stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur de publication (itération {Iter})", iter);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }
            catch (OperationCanceledException) { }
            finally
            {
                _logger.LogInformation("MultiTarget worker arrêté");
            }
        }

        // ─── Cas 1 : PublishAsync<T> — type connu à la compilation ──────────────

        private async Task PublierMessageTypéAsync(int index, CancellationToken ct)
        {
            var id = Guid.NewGuid();

            switch (index)
            {
                case 0:
                    await _producer.PublishAsync(
                        new MessageTransitContext<CarMessage>
                        {
                            MessageId = id.ToString("N"),
                            Message   = new CarMessage { Id = id, Content = "Réservation voiture" }
                        }, cancellationToken: ct);
                    _logger.LogInformation("CarMessage publié → Car | MessageId={Id}", id);
                    break;

                case 1:
                    await _producer.PublishAsync(
                        new MessageTransitContext<HotelMessage>
                        {
                            MessageId = id.ToString("N"),
                            Message   = new HotelMessage { Id = id, Content = "Réservation hôtel" }
                        }, cancellationToken: ct);
                    _logger.LogInformation("HotelMessage publié → Hotel | MessageId={Id}", id);
                    break;

                default:
                    await _producer.PublishAsync(
                        new MessageTransitContext<FlightMessage>
                        {
                            MessageId = id.ToString("N"),
                            Message   = new FlightMessage { Id = id, Content = "Réservation vol" }
                        }, cancellationToken: ct);
                    _logger.LogInformation("FlightMessage publié → Flight | MessageId={Id}", id);
                    break;
            }
        }

        // ─── Cas 2 : PublishMixedBatchAsync — batch hétérogène ──────────────────

        private async Task PublierBatchHeterogeneAsync(CancellationToken ct)
        {
            var id = Guid.NewGuid();

            // Les 3 types sont routés automatiquement vers leurs cibles respectives.
            // Chaque contexte est de type MessageTransitContext<IBookingMessage>
            // avec un Message dont le runtime type est CarMessage / HotelMessage / FlightMessage.
            var batch = new List<MessageTransitContext<IBookingMessage>>
            {
                new() { MessageId = $"{id:N}-car",    Message = new CarMessage    { Id = id, Content = "Batch car"    } },
                new() { MessageId = $"{id:N}-hotel",  Message = new HotelMessage  { Id = id, Content = "Batch hotel"  } },
                new() { MessageId = $"{id:N}-flight", Message = new FlightMessage { Id = id, Content = "Batch flight" } },
            };

            var ids = await _producer.PublishMixedBatchAsync(batch, cancellationToken: ct);
            _logger.LogInformation(
                "Batch hétérogène publié — Car + Hotel + Flight | MessageIds=[{Ids}]",
                string.Join(", ", ids));
        }
    }
}
