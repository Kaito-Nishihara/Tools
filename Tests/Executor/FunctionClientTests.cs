using FluentAssertions;
using SqlScript.Executor.Application;
using SqlScript.Executor.Infrastructure;
using System.Net;
using static System.Net.Mime.MediaTypeNames;

namespace Tests.Executor;
public class FunctionClientTests
{
    [Fact]
    public async Task PostApplyAsync_ShouldSend_Header_AndJsonBody()
    {
        HttpRequestMessage? captured = null;

        var handler = new StubHttpMessageHandler((req) =>
        {
            captured = req;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("OK")
            };
        });

        var http = new HttpClient(handler);
        var client = new FunctionClient(http);

        var cmd = new ApplySqlCommand(
            FunctionUrl: "https://example/api/db/apply",
            FunctionKey: "KEY123",
            Context: "AppDbContext",
            BaseMigrationId: "20260215_Base",
            TargetMigrationId: "20260216_Target",
            Sql: "SELECT 1;",
            Sha256: "ABC",
            DryRun: true,
            Timeout: TimeSpan.FromSeconds(30)
        );

        var res = await client.PostApplyAsync(cmd, CancellationToken.None);

        res.IsSuccess.Should().BeTrue();
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Body.Should().Be("OK");

        captured.Should().NotBeNull();
        captured!.Method.Should().Be(HttpMethod.Post);
        captured.RequestUri!.ToString().Should().Be(cmd.FunctionUrl);

        captured.Headers.Contains("x-functions-key").Should().BeTrue();
        captured.Headers.GetValues("x-functions-key").Single().Should().Be("KEY123");

        var json = await captured.Content!.ReadAsStringAsync();
        json.Should().Contain("\"context\":\"AppDbContext\"");
        json.Should().Contain("\"baseMigrationId\":\"20260215_Base\"");
        json.Should().Contain("\"targetMigrationId\":\"20260216_Target\"");
        json.Should().Contain("\"sql\":\"SELECT 1;\"");
        json.Should().Contain("\"sha256\":\"ABC\"");
        json.Should().Contain("\"dryRun\":true");
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _func;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> func)
        {
            _func = func;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_func(request));
    }
}
