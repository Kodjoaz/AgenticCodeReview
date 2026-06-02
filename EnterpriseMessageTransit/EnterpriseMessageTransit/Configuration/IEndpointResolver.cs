namespace RAMQ.COM.EnterpriseMessageTransit.Configuration
{
    public interface IEndpointResolver
    {
        bool TryResolve(string? target, string? consumer, string? action, out EndpointSettings? endpoint);
    }
}
