namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    public class ReplyToEndpointInfo
    {
        public string EntityName { get; set; } = default!;
        /// <summary>
        /// Time to live, La spécification d’une valeur de durée de vie (TTL) a pour effet que le cluster reste actif pendant un certain temps après la fin de son exécution
        /// </summary>
        /// <see cref="https://learn.microsoft.com/fr-ca/azure/data-factory/concepts-integration-runtime-performance#time-to-live"/>
        public TimeSpan? TTL { get; set; }
        public TimeSpan? ReplyTimeout { get; set; }
    }
}
