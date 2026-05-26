#pragma warning disable
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.Samples.Queue.RequestReply.Consumer;
using RAMQ.Samples.Queue.RequestReply.Message;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace RAMQ.Samples.Queue.RequestReply.Worker
{
    public class DoWork : BackgroundService
    {
        private const int SimulatedUserCount = 1;
        private readonly ILogger<DoWork> _logger;
        private readonly IMessageProducer<RequestMessage> _producer;
        private readonly string _userFileDirectory;
        private const string TargetQueue = "simple.consumercomplete-queue";

        public DoWork(ILogger<DoWork> logger,
                      IMessageProducer<RequestMessage> producer)
        {
            _logger = logger;
            _producer = producer;
            _userFileDirectory = Path.Combine(AppContext.BaseDirectory, "userfiles");
            Directory.CreateDirectory(_userFileDirectory);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RecoverOfflineSessionsAsync(stoppingToken);

            var tasks = new List<Task>();
            for (int i = 1; i <= SimulatedUserCount; i++)
            {
                int userId = i;
                tasks.Add(Task.Run(() => SimulateUserAsync(userId, stoppingToken), stoppingToken));
            }
            await Task.WhenAll(tasks);
        }

        private async Task SimulateUserAsync(int userId, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var correlationId = Guid.NewGuid();
                var ctx = new MessageTransitContext<RequestMessage>
                {
                    MessageId = correlationId.ToString("N"),
                    SessionId = correlationId.ToString("N"),
                    Message = new RequestMessage
                    {
                        Id = correlationId,
                        Content = $"User {userId} - Simple Request Message"
                    }
                };

                var fileName = Path.Combine(_userFileDirectory, $"user{userId}.txt");
                try { await File.WriteAllTextAsync(fileName, ctx.MessageId!, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Persistance fichier échouée {File}", fileName); }

                try
                {
                    _logger.LogInformation("User {UserId}: Envoi request CorrelationId={CorrelationId} Content='{Content}'",
                        userId, ctx.MessageId, ctx.Message?.Content);

                    var sw = Stopwatch.StartNew();

                    var reply = await _producer.GetResponseAsync(
                        ctx,
                        new RequestReplyOptions { Properties = BuildProperties() },
                        cancellationToken: ct);

                    sw.Stop();

                    if (reply != null)
                    {
                        _logger.LogInformation("User {UserId}: Reply reçue CorrelationId={CorrelationId} Status={Status} Duration={Ms}ms",
                            userId,
                            reply.MessageId,
                            reply.Message?.StatusCode,
                            sw.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.LogWarning("User {UserId}: Pas de réponse CorrelationId={CorrelationId}", userId, ctx.MessageId);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogDebug("User {UserId}: Annulation", userId);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "User {UserId}: Erreur request CorrelationId={CorrelationId}", userId, ctx.MessageId);
                }
                finally
                {
                    TryDeleteFile(fileName);
                }

                try { await Task.Delay(TimeSpan.FromMinutes(1), ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task RecoverOfflineSessionsAsync(CancellationToken ct)
        {
            for (int i = 1; i <= SimulatedUserCount; i++)
            {
                var fileName = Path.Combine(_userFileDirectory, $"user{i}.txt");
                if (!File.Exists(fileName)) continue;

                string? messageId = null;
                try { messageId = await File.ReadAllTextAsync(fileName, ct); }
                catch (Exception ex) { _logger.LogWarning(ex, "Lecture offline échouée {File}", fileName); }

                if (string.IsNullOrWhiteSpace(messageId))
                {
                    TryDeleteFile(fileName);
                    continue;
                }

                var gid = Guid.TryParse(messageId, out var parsed) ? parsed : Guid.Empty;

                var ctx = new MessageTransitContext<RequestMessage>
                {
                    MessageId = messageId,
                    SessionId = messageId,
                    Message = new RequestMessage
                    {
                        Id = gid,
                        Content = "Offline recovery"
                    }
                };

                _logger.LogInformation("Offline recovery start CorrelationId={CorrelationId}", messageId);

                var sw = Stopwatch.StartNew();
                MessageTransitContext<MessageTransitResponse>? reply = null;

                try
                {
                    reply = await _producer.GetResponseAsync(
                        ctx,
                        new RequestReplyOptions { Properties = BuildProperties(), EnableOffline = true },
                        cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Offline recovery échec CorrelationId={CorrelationId}", messageId);
                }
                finally
                {
                    TryDeleteFile(fileName);
                }

                sw.Stop();

                if (reply != null)
                {
                    _logger.LogInformation("Offline reply CorrelationId={CorrelationId} Status={Status} Duration={Ms}ms",
                        reply.MessageId,
                        reply.Message?.StatusCode,
                        sw.ElapsedMilliseconds);
                }
                else
                {
                    _logger.LogInformation("Offline reply absente CorrelationId={CorrelationId} Duration={Ms}ms",
                        messageId,
                        sw.ElapsedMilliseconds);
                }
            }
        }

        private static Dictionary<string, object> BuildProperties() =>
            new Dictionary<string, object>
            {
                ["Consumer"] = "RequestReplyProducer",
                ["Action"] = "Request"
            };

        private void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Échec suppression fichier {File}", path);
            }
        }
    }
}