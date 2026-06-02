namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    /// <summary>
    /// Politique de retry pour l'envoi de messages côté Producer.
    /// Distincte de <see cref="ExponentialRetryPolicy"/> qui est réservée au Consumer (relivraisons ASB).
    /// </summary>
    public class ProducerSendRetryPolicy
    {
        /// <summary>
        /// Nombre maximal de tentatives après le premier échec fatal (remplacement du sender inclus).
        /// Défaut : 3.
        /// </summary>
        public int MaxAttempts { get; set; } = 3;

        /// <summary>
        /// Délai initial entre les tentatives. Multiplié par le numéro de tentative (backoff linéaire).
        /// Défaut : 200 ms.
        /// </summary>
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(200);

        /// <summary>
        /// Instance par défaut (3 tentatives, 200 ms de délai initial).
        /// </summary>
        public static ProducerSendRetryPolicy Default => new();
    }
}
