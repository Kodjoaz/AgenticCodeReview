namespace RAMQ.COM.EnterpriseMessageTransit.Serialization
{
    public interface IMessageSerializer
    {
        string Serialize<TMessage>(TMessage obj) where TMessage : class;
        TMessage? Deserialize<TMessage>(string data) where TMessage : class;

        /// <summary>
        /// Désérialise avec un résultat typé qui distingue les cas d'échec
        /// (payload vide, trop volumineux, malformé) au lieu de retourner <c>null</c>.
        /// </summary>
        DeserializationResult<TMessage> DeserializeSafe<TMessage>(string? data) where TMessage : class;
    }
}
