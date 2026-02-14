using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace SqlScript.Generator.Infrastructure;

public interface IDbContextTypeResolver
{
    Type Resolve(string contextName);
}

public sealed class DbContextTypeResolver : IDbContextTypeResolver
{
    public Type Resolve(string contextName)
    {
        var baseDir = AppContext.BaseDirectory;

        foreach (var dll in Directory.EnumerateFiles(baseDir, "*.dll"))
        {
            try { Assembly.LoadFrom(dll); } catch { }
        }
        var ctxTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a =>
            {
                try { return a.GetTypes(); } catch { return Array.Empty<Type>(); }
            })
            .Where(t => typeof(DbContext).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        var matched = ctxTypes.FirstOrDefault(t =>
            string.Equals(t.Name, contextName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t.FullName, contextName, StringComparison.OrdinalIgnoreCase));

        if (matched is null)
        {
            var candidates = string.Join(", ", ctxTypes.Select(t => t.FullName).OrderBy(x => x));
            throw new InvalidOperationException($"DbContext '{contextName}' not found. Candidates: {candidates}");
        }

        return matched;
    }
}