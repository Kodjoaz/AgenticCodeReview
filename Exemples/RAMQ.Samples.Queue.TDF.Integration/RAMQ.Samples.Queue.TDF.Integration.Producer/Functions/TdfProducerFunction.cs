using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RAMQ.Samples.Queue.TDF.Integration.Producer.Services;
using System.Net;
using System.Text.Json;

namespace RAMQ.Samples.Queue.TDF.Integration.Producer.Functions;

public sealed class TdfProducerFunction
{
    private readonly ILogger<TdfProducerFunction> _logger;
    private readonly ITdfProducerService _producerService;

    public TdfProducerFunction(
        ILogger<TdfProducerFunction> logger,
        ITdfProducerService producerService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _producerService = producerService ?? throw new ArgumentNullException(nameof(producerService));
    }

    [Function("PublishInitialTransaction")]
    public async Task<HttpResponseData> PublishInitialTransactionAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tdf/transaction/initial")] HttpRequestData req,
        CancellationToken ct)
    {
        try
        {
            var requestBody = await req.Body.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<TdfTransactionRequest>(requestBody, JsonOptions);

            if (payload is null)
                return CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request payload");

            var result = await _producerService.PublishInitialTransactionAsync(payload, ct);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in PublishInitialTransaction");
            return CreateErrorResponse(req, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing initial transaction");
            return CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to publish transaction");
        }
    }

    [Function("PublishCorrelation")]
    public async Task<HttpResponseData> PublishCorrelationAsync(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "tdf/transaction/correlation")] HttpRequestData req,
        CancellationToken ct)
    {
        try
        {
            var requestBody = await req.Body.ReadAsStringAsync();
            var payload = JsonSerializer.Deserialize<TdfCorrelationRequest>(requestBody, JsonOptions);

            if (payload is null)
                return CreateErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request payload");

            var result = await _producerService.PublishCorrelationAsync(payload, ct);
            var response = req.CreateResponse(HttpStatusCode.Accepted);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation error in PublishCorrelation");
            return CreateErrorResponse(req, HttpStatusCode.BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing correlation");
            return CreateErrorResponse(req, HttpStatusCode.InternalServerError, "Failed to publish correlation");
        }
    }

    private static HttpResponseData CreateErrorResponse(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message)
    {
        var response = req.CreateResponse(statusCode);
        var errorPayload = new { error = message, timestamp = DateTime.UtcNow };
        response.Headers.Add("Content-Type", "application/json");
        response.WriteString(JsonSerializer.Serialize(errorPayload, JsonOptions));
        return response;
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
