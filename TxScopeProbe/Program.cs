using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Transactions;
TransactionManager.ImplicitDistributedTransactions = true;
await Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(cfg =>
    {
        cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
    })
    .ConfigureServices((ctx, services) =>
    {
        services.AddDbContext<Db1Context>(opt =>
            opt.UseSqlServer(ctx.Configuration.GetConnectionString("Db1")));

        services.AddDbContext<Db2Context>(opt =>
            opt.UseSqlServer(ctx.Configuration.GetConnectionString("Db2")));

        services.AddScoped<ProbeRunner>();

        // ★これを追加
        services.AddHostedService<StartupHostedService>();
    })
    .RunConsoleAsync();

public sealed class ProbeRunner
{
    private readonly Db1Context _db1;
    private readonly Db2Context _db2;
    private readonly IServiceProvider _sp;

    public ProbeRunner(IServiceProvider sp,Db1Context db1, Db2Context db2)
    {
        _db1 = db1;
        _db2 = db2;
        _sp = sp;
    }

    public async Task RunAsync(CancellationToken ct = default)
    {
        await EnsureTablesAsync(ct);

        var marker = Guid.NewGuid().ToString("N");

        Console.WriteLine($"Marker: {marker}");
        Console.WriteLine("=== BEFORE ===");
        await DumpCountsAsync(marker, ct);

        try
        {
            using var scope = new TransactionScope(
                TransactionScopeOption.Required,
                new TransactionOptions
                {
                    IsolationLevel = IsolationLevel.ReadCommitted,
                    Timeout = TransactionManager.DefaultTimeout
                },
                TransactionScopeAsyncFlowOption.Enabled);

            await _db1.Database.OpenConnectionAsync(ct);
            await _db2.Database.OpenConnectionAsync(ct);

            _db1.TxProbe.Add(new TxProbe { Marker = marker, DbName = "DB1", CreatedAtUtc = DateTime.UtcNow });
            await _db1.SaveChangesAsync(ct);

            _db2.TxProbe.Add(new TxProbe { Marker = marker, DbName = "DB2", CreatedAtUtc = DateTime.UtcNow });
            await _db2.SaveChangesAsync(ct);

            // ▼ロールバック確認したいなら例外（Completeしない）
            //throw new InvalidOperationException("INTENTIONAL FAILURE");

            // ▼コミット確認したいならこれ
            scope.Complete();
        }
        catch (Exception ex)
        {
            Console.WriteLine("=== EXCEPTION ===");
            Console.WriteLine(ex.Message);
        }

        Console.WriteLine("=== AFTER ===");

        // AFTER は必ず新しい DbContext で確認（8525回避）
        using var afterScope = _sp.CreateScope();
        var freshDb1 = afterScope.ServiceProvider.GetRequiredService<Db1Context>();
        var freshDb2 = afterScope.ServiceProvider.GetRequiredService<Db2Context>();

        var a1 = await freshDb1.TxProbe.CountAsync(x => x.Marker == marker, ct);
        var a2 = await freshDb2.TxProbe.CountAsync(x => x.Marker == marker, ct);

        Console.WriteLine($"DB1 count(marker) = {a1}");
        Console.WriteLine($"DB2 count(marker) = {a2}");
    }

    private async Task EnsureTablesAsync(CancellationToken ct)
    {
        // ここは “テスト用のテーブルが無ければ作る” だけ（Azure SQLでも動く単純DDL）
        const string sql = @"
IF OBJECT_ID(N'dbo.TxProbe', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TxProbe
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Marker NVARCHAR(64) NOT NULL,
        DbName NVARCHAR(16) NOT NULL,
        CreatedAtUtc DATETIME2 NOT NULL
    );
    CREATE INDEX IX_TxProbe_Marker ON dbo.TxProbe(Marker);
END
";

        await _db1.Database.ExecuteSqlRawAsync(sql, ct);
        await _db2.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private async Task DumpCountsAsync(string marker, CancellationToken ct)
    {
        var c1 = await _db1.TxProbe.CountAsync(x => x.Marker == marker, ct);
        var c2 = await _db2.TxProbe.CountAsync(x => x.Marker == marker, ct);

        Console.WriteLine($"DB1 count(marker) = {c1}");
        Console.WriteLine($"DB2 count(marker) = {c2}");
    }
}

public sealed class Db1Context : DbContext
{
    public Db1Context(DbContextOptions<Db1Context> options) : base(options) { }
    public DbSet<TxProbe> TxProbe => Set<TxProbe>();
}

public sealed class Db2Context : DbContext
{
    public Db2Context(DbContextOptions<Db2Context> options) : base(options) { }
    public DbSet<TxProbe> TxProbe => Set<TxProbe>();
}

public sealed class TxProbe
{
    public int Id { get; set; }
    public string Marker { get; set; } = default!;
    public string DbName { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class StartupHostedService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IHostApplicationLifetime _lifetime;

    public StartupHostedService(IServiceProvider sp, IHostApplicationLifetime lifetime)
    {
        _sp = sp;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _sp.CreateScope();
            var runner = scope.ServiceProvider.GetRequiredService<ProbeRunner>();
            await runner.RunAsync(stoppingToken);
        }
        finally
        {
            // 1回実行したらホスト終了
            _lifetime.StopApplication();
        }
    }
}