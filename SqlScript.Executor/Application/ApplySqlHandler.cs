using SqlScript.Executor.Infrastructure;

namespace SqlScript.Executor.Application;

public sealed class ApplySqlHandler
{
    private readonly IFunctionClient _client;

    public ApplySqlHandler(IFunctionClient client)
    {
        _client = client;
    }

    public async Task<int> HandleAsync(ApplySqlCommand command, CancellationToken ct)
    {
        var result = await _client.PostApplyAsync(command, ct);

        Console.WriteLine($"HTTP: {(int)result.StatusCode} {result.StatusCode}");
        Console.WriteLine(result.Body);

        return result.IsSuccess ? 0 : 1;
    }
}
