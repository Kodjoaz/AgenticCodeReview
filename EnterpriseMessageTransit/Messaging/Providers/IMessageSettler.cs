using RAMQ.COM.EnterpriseMessageTransit.Exceptions;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    /// <summary>
    /// Responsabilité unique : règlement (settlement) d'un message Service Bus.
    /// Un consumer découplé de l'envoi peut n'injecter que cette interface.
    /// </summary>
    public interface IMessageSettler
    {
        Task CompleteMessageAsync(CancellationToken cancellationToken = default);
        Task ImmediateRetryAsync(ImmediateRetryException exception, CancellationToken cancellationToken = default);
        Task ExponentialRetryAsync(ExponentialRetryException exception, CancellationToken cancellationToken = default);
        Task DeadLetterMessageAsync(Exception exception, CancellationToken cancellationToken = default);
    }
}
