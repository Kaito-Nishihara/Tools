using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SqlScript.Generator.Infrastructure;
namespace Tests.Generator;
public class DbContextTypeResolverTests
{
    [Fact]
    public void Resolve_ShouldFind_DbContext_InLoadedAssemblies()
    {
        // テストアセンブリにある DbContext を解決できること
        var resolver = new DbContextTypeResolver();

        var t = resolver.Resolve(nameof(FakeDbContext));

        t.Should().Be(typeof(FakeDbContext));
    }

    // ★ テスト専用 DbContext
    private sealed class FakeDbContext : DbContext
    {
        public FakeDbContext(DbContextOptions<FakeDbContext> options) : base(options) { }
    }
}