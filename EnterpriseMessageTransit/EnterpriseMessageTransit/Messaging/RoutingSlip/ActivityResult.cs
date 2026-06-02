using RAMQ.COM.EnterpriseMessageTransit.Exceptions;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.RoutingSlip
{
    /// <summary>
    /// Ce que votre activité retourne pour indiquer au framework quoi faire ensuite.
    /// Utilisez uniquement les factory methods statiques — ne pas sous-classer directement.
    /// </summary>
    public abstract class ActivityResult
    {
        private ActivityResult() { } // Scellé aux sous-classes internes uniquement

        // ─── Factory methods (API publique) ─────────────────────────────────

        /// <summary>
        /// "J'ai terminé, passe à l'étape suivante."
        /// Le paramètre optionnel enrichit les variables partagées lues par les étapes suivantes.
        /// </summary>
        /// <example>
        ///   return ActivityResult.Next();
        ///   return ActivityResult.Next(vars => { vars["DateValidation"] = DateTime.UtcNow; vars["Montant"] = 250m; });
        /// </example>
        public static ActivityResult Next(Action<IDictionary<string, object>>? enrichVariables = null)
            => new NextResult(enrichVariables);

        /// <summary>
        /// "J'ai terminé ET c'est la fin du workflow."
        /// À utiliser si l'activité décide elle-même de terminer le slip avant la dernière étape.
        /// Dans la plupart des cas, le framework détecte la fin automatiquement.
        /// </summary>
        public static ActivityResult Complete()
            => new CompleteResult();

        /// <summary>
        /// "Erreur permanente — envoie ce message en DLQ."
        /// Les étapes suivantes ne seront pas exécutées.
        /// Si des compensateurs sont enregistrés, ils sont déclenchés en ordre inverse.
        /// </summary>
        public static ActivityResult Fault(Exception exception)
            => new FaultResult(exception ?? throw new ArgumentNullException(nameof(exception)));

        /// <summary>
        /// "Erreur transitoire — réessaie immédiatement."
        /// Service Bus redelivre le message sans délai (ImmediateRetryException EMT).
        /// À utiliser pour des erreurs de courte durée : lock optimiste perdu, contention transitoire.
        /// </summary>
        public static ActivityResult RetryImmediate(string reason)
            => new RetryImmediateResult(reason ?? throw new ArgumentNullException(nameof(reason)));

        /// <summary>
        /// "Erreur transitoire — réessaie avec backoff exponentiel."
        /// Service Bus abandonne le message et applique le délai configuré dans ExponentialRetryPolicy.
        /// À utiliser pour des erreurs qui prennent du temps à se résoudre : service indisponible, timeout HTTP.
        /// </summary>
        public static ActivityResult RetryExponential(string reason, Exception? innerException = null)
            => new RetryExponentialResult(reason ?? throw new ArgumentNullException(nameof(reason)), innerException);

        // ─── Sous-types internes (sealed) ────────────────────────────────────

        internal sealed class NextResult : ActivityResult
        {
            public Action<IDictionary<string, object>>? EnrichVariables { get; }
            internal NextResult(Action<IDictionary<string, object>>? enrichVariables)
                => EnrichVariables = enrichVariables;
        }

        internal sealed class CompleteResult : ActivityResult { }

        internal sealed class FaultResult : ActivityResult
        {
            public Exception Exception { get; }
            internal FaultResult(Exception exception) => Exception = exception;
        }

        internal sealed class RetryImmediateResult : ActivityResult
        {
            public string Reason { get; }
            internal RetryImmediateResult(string reason) => Reason = reason;
        }

        internal sealed class RetryExponentialResult : ActivityResult
        {
            public string Reason { get; }
            public Exception? InnerException { get; }
            internal RetryExponentialResult(string reason, Exception? innerException)
            {
                Reason = reason;
                InnerException = innerException;
            }
        }
    }
}
