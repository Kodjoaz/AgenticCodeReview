using Microsoft.Extensions.Logging;
using RAMQ.COM.EnterpriseMessageTransit.Configuration;
using RAMQ.COM.EnterpriseMessageTransit.Messaging;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Enum;
using RAMQ.COM.EnterpriseMessageTransit.Messaging.Producer;
using RAMQ.Samples.Queue.TDF.Integration.Consumer.Messages;

namespace RAMQ.Samples.Queue.TDF.Integration.Producer.Services;

public sealed class TdfProducerService : ITdfProducerService
{
    private readonly ILogger<TdfProducerService> _logger;
    private readonly IMessageProducer<TdfTransactionCommand> _producer;

    public TdfProducerService(
        ILogger<TdfProducerService> logger,
        IMessageProducer<TdfTransactionCommand> producer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producer = producer ?? throw new ArgumentNullException(nameof(producer));
    }

    public async Task<TdfPublishResult> PublishInitialTransactionAsync(
        TdfTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["SessionId"] = request.SessionId,
            ["NumeroEchange"] = request.NumeroEchange,
            ["CorrelationId"] = request.CorrelationId
        });

        _logger.LogInformation(
            "Publishing initial TDF transaction. SessionId={SessionId}, NumeroEchange={NumeroEchange}",
            request.SessionId, request.NumeroEchange);

        var tokens = new List<TokenMessage>();
        if (!string.IsNullOrEmpty(request.BlobReference))
        {
            ValidateBlobReference(request.BlobReference);
            tokens.Add(new TokenMessage
            {
                Kind = TokenKind.File,
                Reference = request.BlobReference,
                ContentType = request.ContentType ?? "application/octet-stream",
                SizeBytes = request.SizeBytes
            });
        }

        var context = new MessageTransitContext<TdfTransactionCommand>
        {
            SessionId = request.SessionId,
            CorrelationId = request.CorrelationId,
            MessageType = nameof(TdfTransactionCommand),
            Variables = new Dictionary<string, object> { ["step"] = "tdf.envoi" },
            Tokens = tokens,
            Message = new TdfTransactionCommand(
                AuthorizationToken: request.AuthorizationToken,
                NumeroEchange: request.NumeroEchange,
                BlobReference: request.BlobReference ?? string.Empty)
        };

        var result = await _producer.PublishAsync(context, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Initial transaction published. MessageId={MessageId}, BlobRef={BlobRef}",
            result.MessageId, request.BlobReference);

        return new TdfPublishResult(result.MessageId, request.SessionId, DateTime.UtcNow);
    }

    public async Task<TdfPublishResult> PublishCorrelationAsync(
        TdfCorrelationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateCorrelationRequest(request);

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["SessionId"] = request.SessionId,
            ["NumeroEchange"] = request.NumeroEchange,
            ["CorrelationId"] = request.CorrelationId
        });

        _logger.LogInformation(
            "Publishing correlation confirmation. SessionId={SessionId}, AccuseReception={AccuseReception}",
            request.SessionId, request.AccuseReception);

        var context = new MessageTransitContext<TdfTransactionCommand>
        {
            SessionId = request.SessionId,
            CorrelationId = request.CorrelationId,
            MessageType = nameof(TdfTransactionCommand),
            Variables = new Dictionary<string, object> { ["step"] = "tdf.correller" },
            Message = new TdfTransactionCommand(
                AuthorizationToken: request.AuthorizationToken,
                NumeroEchange: request.NumeroEchange,
                AccuseReception: request.AccuseReception)
        };

        var result = await _producer.PublishAsync(context, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Correlation published. MessageId={MessageId}, AccuseReception={AccuseReception}",
            result.MessageId, request.AccuseReception);

        return new TdfPublishResult(result.MessageId, request.SessionId, DateTime.UtcNow);
    }

    private static void ValidateRequest(TdfTransactionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            throw new ArgumentException("SessionId is required", nameof(request.SessionId));
        if (string.IsNullOrWhiteSpace(request.CorrelationId))
            throw new ArgumentException("CorrelationId is required", nameof(request.CorrelationId));
        if (string.IsNullOrWhiteSpace(request.AuthorizationToken))
            throw new ArgumentException("AuthorizationToken is required", nameof(request.AuthorizationToken));
        if (string.IsNullOrWhiteSpace(request.NumeroEchange))
            throw new ArgumentException("NumeroEchange is required", nameof(request.NumeroEchange));
    }

    private static void ValidateCorrelationRequest(TdfCorrelationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId))
            throw new ArgumentException("SessionId is required", nameof(request.SessionId));
        if (string.IsNullOrWhiteSpace(request.CorrelationId))
            throw new ArgumentException("CorrelationId is required", nameof(request.CorrelationId));
        if (string.IsNullOrWhiteSpace(request.AuthorizationToken))
            throw new ArgumentException("AuthorizationToken is required", nameof(request.AuthorizationToken));
        if (string.IsNullOrWhiteSpace(request.NumeroEchange))
            throw new ArgumentException("NumeroEchange is required", nameof(request.NumeroEchange));
        if (string.IsNullOrWhiteSpace(request.AccuseReception))
            throw new ArgumentException("AccuseReception is required", nameof(request.AccuseReception));
    }

    private static void ValidateBlobReference(string blobReference)
    {
        if (blobReference.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            blobReference.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "BlobReference must be relative path (not absolute URL) for Claim Check pattern",
                nameof(blobReference));
        }
    }
}

