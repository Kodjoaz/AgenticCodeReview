namespace RAMQ.COM.EnterpriseMessageTransit.Messaging
{
    /// <summary>
    /// Constantes partagées pour les clés de propriétés applicatives des messages.
    /// Placée dans la couche Messaging (neutre) pour éviter que Producer dépende
    /// de la couche Azure et que Provider dépende de la couche Producer.
    /// </summary>
    public static class MessagePropertyKeys
    {
        public const string Consumer = "Consumer";
        public const string Action   = "Action";
    }
}
