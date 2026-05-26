using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.Samples.Queue.MultiTarget.Producer;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RAMQ.Samples.Queue.MultiTarget.Worker
{
    /// <summary>
    /// BackgroundService pour la publication multi-target de messages vers Azure Service Bus.
    /// Publie des messages en round-robin vers "Car", "Hotel", "Flight" toutes les 30 secondes.
    /// </summary>
    public class DoWork : BackgroundService
    {
        private static readonly string[] _targets = ["Car", "Hotel", "Flight"];
        private static long _roundRobinCounter = -1;

        private readonly ILogger<DoWork> _logger;
        private readonly MultiTargetPublicationService _publicationService;

        public DoWork(
            ILogger<DoWork> logger,
            MultiTargetPublicationService publicationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _publicationService = publicationService ?? throw new ArgumentNullException(nameof(publicationService));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MultiTarget worker démarrant");

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var id = Guid.NewGuid();
                    var target = GetNextTarget();
                    var content = $"Message {id:N} vers {target}";

                    try
                    {
                        await _publicationService.PublierAsync(target, id, content, stoppingToken);
                        _logger.LogInformation("Message publié avec succès vers {Target} | MessageId={MessageId}", target, id);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Publication annulée pour MessageId={MessageId}", id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur lors de la publication vers {Target} | MessageId={MessageId}", target, id);
                    }

                    // Délai configurable (actuellement 30 secondes)
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Arrêt du worker demandé");
                    }
                }
            }
            finally
            {
                _logger.LogInformation("MultiTarget worker arrêté");
            }
        }

        /// <summary>
        /// Retourne le prochain target en mode round-robin.
        /// Utilise un compteur `long` pour éviter les débordements d'entier.
        /// </summary>
        private static string GetNextTarget()
        {
            var index = (int)(Interlocked.Increment(ref _roundRobinCounter) % _targets.Length);
            return _targets[index];
        }
    }
}
