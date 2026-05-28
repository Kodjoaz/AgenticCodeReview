namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Interface composite (backward-compat) : regroupe réception et settlement.
    /// Pour les nouveaux composants, préférer <see cref="IMessageReceiver"/> ou <see cref="IMessageSettler"/>.
    /// </summary>
    public interface IMessageActions : IMessageReceiver, IMessageSettler { }
}
