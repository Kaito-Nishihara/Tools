using System.Net;
using System.Text.Json;
using FunctionApp.Application;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace FunctionApp.Functions;

public sealed record ApplyMigrationSqlRequest(
    string Context,
    string BaseMigrationId,
    string TargetMigrationId,
    string Sql,
    string Sha256,
    bool DryRun
);

public sealed class ApplyMigrationSqlFunction
{
    private readonly ApplyMigrationHandler _handler;
    private readonly ILogger _log;

    public ApplyMigrationSqlFunction(ApplyMigrationHandler handler, ILoggerFactory loggerFactory)
    {
        _handler = handler;
        _log = loggerFactory.CreateLogger<ApplyMigrationSqlFunction>();
    }

    [Function("ApplyMigrationSql")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "db/apply-migration-sql")]
        HttpRequestData req,
        CancellationToken ct)
    {
        try
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync(ct);

            var request = JsonSerializer.Deserialize<ApplyMigrationSqlRequest>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (request is null)
                return Bad(req, "Invalid JSON.");

            var cmd = new ApplyMigrationCommand(
                Context: request.Context,
                BaseMigrationId: request.BaseMigrationId,
                TargetMigrationId: request.TargetMigrationId,
                Sql: request.Sql,
                Sha256: request.Sha256,
                DryRun: request.DryRun);

            var (ok, message) = await _handler.HandleAsync(cmd, ct);

            return ok ? Ok(req, message) : Bad(req, message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unhandled error");
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
