using System.Collections.Concurrent;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// État du circuit breaker.
    /// </summary>
    internal enum CircuitState
    {
        /// <summary>Le circuit est fermé — les opérations passent normalement.</summary>
        Closed,

        /// <summary>Le circuit est ouvert — les opérations sont rejetées immédiatement.</summary>
        Open,

        /// <summary>Le circuit est semi-ouvert — une seule opération est autorisée pour tester la reprise.</summary>
        HalfOpen
    }

    /// <summary>
    /// Circuit breaker léger, thread-safe, par clé d'entité Service Bus.
    /// Chaque entité (queue/topic) a son propre état de circuit indépendant.
    /// 
    /// Workflow :
    ///   Closed → (FailureThreshold échecs consécutifs) → Open
    ///   Open → (après OpenDuration) → HalfOpen
    ///   HalfOpen → succès → Closed / échec → Open
    /// </summary>
    internal class CircuitBreakerManager
    {
        private readonly CircuitBreakerOptions _options;
        private readonly IMetricsProvider?     _metrics;
        private readonly ConcurrentDictionary<string, CircuitEntry> _circuits = new();

        public CircuitBreakerManager(CircuitBreakerOptions options, IMetricsProvider? metrics = null)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            if (options.FailureThreshold <= 0)
                throw new ArgumentOutOfRangeException(nameof(options),
                    $"FailureThreshold doit être supérieur à 0 (valeur reçue : {options.FailureThreshold}).");
            if (options.OpenDuration <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(options),
                    $"OpenDuration doit être une durée positive (valeur reçue : {options.OpenDuration}).");
            _metrics = metrics;
        }

        /// <summary>
        /// Vérifie si l'opération est autorisée pour l'entité donnée.
        /// En état Open : lève <see cref="CircuitBreakerOpenException"/> immédiatement.
        /// En état HalfOpen : autorise une seule tentative (test probe).
        /// </summary>
        /// <param name="entityName">Nom de l'entité Service Bus (queue ou topic).</param>
        /// <exception cref="CircuitBreakerOpenException">Si le circuit est ouvert et que la période de cooldown n'est pas expirée.</exception>
        public void EnsureCircuitAllows(string entityName)
        {
            var entry = _circuits.GetOrAdd(entityName, _ => new CircuitEntry());

            lock (entry.SyncRoot)
            {
                switch (entry.State)
                {
                    case CircuitState.Closed:
                        return; // OK

                    case CircuitState.Open:
                        if (DateTimeOffset.UtcNow >= entry.OpenedUntil)
                        {
                            entry.State = CircuitState.HalfOpen;
                            _metrics?.SetCircuitState(entityName, 2);
                            _metrics?.IncrementCircuitTransition(entityName, "Open", "HalfOpen");
                            return;
                        }
                        throw new CircuitBreakerOpenException(entityName, entry.OpenedUntil);

                    case CircuitState.HalfOpen:
                        return; // Un seul test probe autorisé
                }
            }
        }

        /// <summary>
        /// Signale un succès : réinitialise le compteur et ferme le circuit.
        /// </summary>
        public void RecordSuccess(string entityName)
        {
            var entry = _circuits.GetOrAdd(entityName, _ => new CircuitEntry());

            lock (entry.SyncRoot)
            {
                var prev = entry.State;
                entry.ConsecutiveFailures = 0;
                entry.State = CircuitState.Closed;
                if (prev != CircuitState.Closed)
                {
                    _metrics?.SetCircuitState(entityName, 0);
                    _metrics?.IncrementCircuitTransition(entityName, prev.ToString(), "Closed");
                }
            }
        }

        /// <summary>
        /// Signale un échec : incrémente le compteur et ouvre le circuit si le seuil est atteint.
        /// </summary>
        public void RecordFailure(string entityName)
        {
            var entry = _circuits.GetOrAdd(entityName, _ => new CircuitEntry());

            lock (entry.SyncRoot)
            {
                entry.ConsecutiveFailures++;

                if (entry.State == CircuitState.HalfOpen)
                {
                    entry.State = CircuitState.Open;
                    entry.OpenedUntil = DateTimeOffset.UtcNow.Add(_options.OpenDuration);
                    _metrics?.SetCircuitState(entityName, 1);
                    _metrics?.IncrementCircuitTransition(entityName, "HalfOpen", "Open");
                }
                else if (entry.ConsecutiveFailures >= _options.FailureThreshold)
                {
                    entry.State = CircuitState.Open;
                    entry.OpenedUntil = DateTimeOffset.UtcNow.Add(_options.OpenDuration);
                    _metrics?.SetCircuitState(entityName, 1);
                    _metrics?.IncrementCircuitTransition(entityName, "Closed", "Open");
                }
            }
        }

        /// <summary>
        /// Retourne l'état courant du circuit pour une entité donnée (diagnostic/monitoring).
        /// </summary>
        public CircuitState GetState(string entityName)
        {
            if (_circuits.TryGetValue(entityName, out var entry))
            {
                lock (entry.SyncRoot)
                {
                    if (entry.State == CircuitState.Open && DateTimeOffset.UtcNow >= entry.OpenedUntil)
                    {
                        return CircuitState.HalfOpen;
                    }
                    return entry.State;
                }
            }
            return CircuitState.Closed;
        }

        private class CircuitEntry
        {
            public readonly object SyncRoot = new();
            public CircuitState State = CircuitState.Closed;
            public int ConsecutiveFailures;
            public DateTimeOffset OpenedUntil;
        }
    }
}
