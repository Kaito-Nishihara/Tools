using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SqlScript.Executor.Application;

namespace SqlScript.Executor.Infrastructure;

public sealed record FunctionResponse(bool IsSuccess, HttpStatusCode StatusCode, string Body);

public interface IFunctionClient
{
    Task<FunctionResponse> PostApplyAsync(ApplySqlCommand command, CancellationToken ct);
}

public sealed class FunctionClient : IFunctionClient
{
    private readonly HttpClient _http;

    public FunctionClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<FunctionResponse> PostApplyAsync(ApplySqlCommand command, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(command.Timeout);

        // Function Key: クエリでもヘッダでもOK。ここはヘッダで送る（推奨）
        // もし AuthorizationLevel.Function を使っていてクエリ必須なら URL に ?code= を付ける方式に変えてください。
        _http.DefaultRequestHeaders.Remove("x-functions-key");
        _http.DefaultRequestHeaders.Add("x-functions-key", command.FunctionKey);

        var payload = new
        {
            context = command.Context,
            baseMigrationId = command.BaseMigrationId,
            targetMigrationId = command.TargetMigrationId,
            sql = command.Sql,
            sha256 = command.Sha256,
            dryRun = command.DryRun
        };

        // SQLが大きい場合に備えて（既定より大きいJSONでも送れるように）
        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        HttpResponseMessage resp;
        try
        {
            resp = await _http.PostAsJsonAsync(command.FunctionUrl, payload, jsonOptions, cts.Token);
        }
        catch (Exception ex)
        {
            return new FunctionResponse(false, 0, $"Request failed: {ex}");
        }

        var body = await resp.Content.ReadAsStringAsync(cts.Token);
        return new FunctionResponse(resp.IsSuccessStatusCode, resp.StatusCode, body);
    }
}
