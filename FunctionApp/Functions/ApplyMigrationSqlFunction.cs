using FunctionApp.Application;
using FunctionApp.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace FunctionApp.Functions;

public class ApplyMigrationSqlFunction
{
    private readonly ApplyMigrationHandler _handler;
    private readonly ILogger _logger;

    public ApplyMigrationSqlFunction(
        ApplyMigrationHandler handler,
        ILoggerFactory loggerFactory)
    {
        _handler = handler;
        _logger = loggerFactory.CreateLogger<ApplyMigrationSqlFunction>();
    }

    [Function("ApplyMigrationSql")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")]
        HttpRequestData req)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            var request = System.Text.Json.JsonSerializer.Deserialize<ApplyMigrationSqlRequest>(body,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request is null)
                return Bad(req, "Invalid JSON");

            var result = await _handler.HandleAsync(request);

            if (!result.Success)
                return Bad(req, result.Message);

            return Ok(req, result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error");
            return Bad(req, ex.Message);
        }
    }

    private static HttpResponseData Ok(HttpRequestData req, string msg)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.WriteString(msg);
        return res;
    }

    private static HttpResponseData Bad(HttpRequestData req, string msg)
    {
        var res = req.CreateResponse(HttpStatusCode.BadRequest);
        res.WriteString(msg);
        return res;
    }
}