namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers
{
    public interface IMessagingAdapter : IMessageActions
    {
        IMessageTransit GetMessage();
    }
}
