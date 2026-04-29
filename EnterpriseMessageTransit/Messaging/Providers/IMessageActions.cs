using RAMQ.COM.EnterpriseMessageTransit.Exceptions;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    public interface IMessageActions
    {
        /// <summary>
        /// Permet de propager les métadonnées d’invocation
        /// </summary>
        void SetInvocationMetadata(string? target, string? consumer, string? action);
        void BindContext(object message, object actions); // Utilise object pour l’abstraction
        // Preferred typed overload to reduce transport coupling. Implementers should support this overload
        // to allow callers to bind using the `IMessageTransit` abstraction instead of platform types.
        void BindContext(IMessageTransit message, object actions);

        Task CompleteMessageAsync(CancellationToken cancellationToken = default);
        Task ImmediateRetryAsync(ImmediateRetryException exception, CancellationToken cancellationToken = default);
        Task ExponentialRetryAsync(ExponentialRetryException exception, CancellationToken cancellationToken = default);
        Task DeadLetterMessageAsync(Exception exception, CancellationToken cancellationToken = default);
    }
}
