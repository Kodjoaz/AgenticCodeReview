using Microsoft.Extensions.DependencyInjection;

namespace RAMQ.Samples.Topic.RoutingSlip.Booking.Worker
{
    public static class ApplicationInsightsExtensions
    {
        public static IServiceCollection ConfigureFunctionsApplicationInsights(this IServiceCollection services)
        {
            // Extension factice pour alignement avec l'activateur.
            // Si une logique spécifique est requise, l'ajouter ici.
            return services;
        }
    }
}
