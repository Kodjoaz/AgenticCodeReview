using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace RAMQ.COM.EnterpriseMessageTransit.Messaging.Providers.Azure
{
    /// <summary>
    /// Singleton cache for ServiceBusSender instances to allow reuse across stateless hosts (Azure Functions).
    /// </summary>
    public class ServiceBusSenderCache : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, ServiceBusSender> _cache = new();
        private readonly ConcurrentDictionary<string, object> _locks = new();
        private readonly ILogger<ServiceBusSenderCache>? _logger;

        public ServiceBusSenderCache(ILogger<ServiceBusSenderCache>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Retourne un <see cref="ServiceBusSender"/> mis en cache pour le couple (namespace, entité).
        /// La clé composite "{FullyQualifiedNamespace}|{entityName}" garantit l'isolation entre namespaces
        /// lorsque plusieurs producers ciblent des namespaces Service Bus différents.
        /// </summary>
        public ServiceBusSender GetOrCreate(ServiceBusClient client, string entityName)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new ArgumentNullException(nameof(entityName));
            }

            // Clé composite : namespace + entité pour éviter les collisions entre namespaces distincts.
            var key = GetKey(client, entityName);
            return _cache.GetOrAdd(key, k => client.CreateSender(entityName));
        }

        private static string GetKey(ServiceBusClient client, string entityName) => $"{client.FullyQualifiedNamespace}|{entityName}";

        /// <summary>
        /// Replace the cached sender for the given client/entity with a newly created one.
        /// This method is thread-safe: the lock serializes replacements for the same key,
        /// and the atomic _cache[] assignment ensures GetOrCreate never returns a stale sender.
        /// The old sender is disposed asynchronously in a background task to avoid blocking the caller.
        /// </summary>
        public ServiceBusSender ReplaceSender(ServiceBusClient client, string entityName)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            if (string.IsNullOrWhiteSpace(entityName))
            {
                throw new ArgumentNullException(nameof(entityName));
            }

            var key = GetKey(client, entityName);
            var gate = _locks.GetOrAdd(key, k => new object());

            lock (gate)
            {
                _cache.TryGetValue(key, out var oldSender);

                var newSender = client.CreateSender(entityName);
                // Atomic upsert : GetOrCreate concurrent sur la même clé verra immédiatement le nouveau sender.
                _cache[key] = newSender;

                // Dispose de l'ancien sender hors du verrou, en arrière-plan.
                if (oldSender != null)
                {
                    _ = DisposeSenderAsync(oldSender);
                }

                return newSender;
            }
        }

        private async Task DisposeSenderAsync(ServiceBusSender sender)
        {
            try
            {
                await sender.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error disposing ServiceBusSender");
            }
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var kv in _cache)
            {
                try
                {
                    await kv.Value.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error disposing ServiceBusSender for entity {Entity}", kv.Key);
                }
            }
            _cache.Clear();
            _locks.Clear();
        }
    }
}
