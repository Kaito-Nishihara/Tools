using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace FunctionApp.Infrastructure;

public interface IDbContextFactory
{
    DbContext Create(string contextName);
}

public sealed class DbContextFactory : IDbContextFactory
{
    private readonly IConfiguration _config;

    public DbContextFactory(IConfiguration config)
    {
        _config = config;
    }

    public DbContext Create(string contextName)
    {
        var cs = _config.GetConnectionString("TargetDb");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("Missing connection string 'TargetDb'.");

        // 参照しているアセンブリが未ロードの可能性があるので、出力先dllをロード
        LoadAllAssembliesFromOutput();

        var ctxType = ResolveDbContextType(contextName);

        // DbContextOptions<TContext> を作る
        var optionsBuilderType = typeof(DbContextOptionsBuilder<>).MakeGenericType(ctxType);
        var optionsBuilder = (DbContextOptionsBuilder)Activator.CreateInstance(optionsBuilderType)!;
        optionsBuilder.UseSqlServer(cs);

        var options = optionsBuilder.Options;

        // DbContext ctor(DbContextOptions<T>) を呼ぶ
        var db = (DbContext?)Activator.CreateInstance(ctxType, options);
        if (db is null)
            throw new InvalidOperationException($"Failed to create DbContext: {ctxType.FullName}");

        return db;
    }

    private static Type ResolveDbContextType(string name)
    {
        var ctxTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
            })
            .Where(t => typeof(DbContext).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var matched = ctxTypes.FirstOrDefault(t =>
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.FullName, name, StringComparison.OrdinalIgnoreCase));

        if (matched is null)
        {
            var candidates = string.Join(", ", ctxTypes.Select(t => t.FullName).OrderBy(x => x));
            throw new InvalidOperationException($"DbContext '{name}' not found. Candidates: {candidates}");
        }

        return matched;
    }

    private static void LoadAllAssembliesFromOutput()
    {
        var baseDir = AppContext.BaseDirectory;

        foreach (var dll in Directory.EnumerateFiles(baseDir, "*.dll"))
        {
            try { Assembly.LoadFrom(dll); } catch { /* ignore */ }
        }
    }
}
