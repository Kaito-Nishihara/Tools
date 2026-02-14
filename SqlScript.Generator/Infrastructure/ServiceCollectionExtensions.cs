using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace SqlScript.Generator.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddEfCoreFactory(
        this IServiceCollection services,
        Type dbContextType,
        string connectionString,
        string? migrationsAssembly)
    {
        // AddDbContextFactory<TContext>(...)
        var method = typeof(EntityFrameworkServiceCollectionExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .First(m => m.Name == "AddDbContextFactory"
                        && m.IsGenericMethodDefinition
                        && m.GetParameters().Length >= 2);

        var generic = method.MakeGenericMethod(dbContextType);

        // options => UseSqlServer(...)
        Action<IServiceProvider, DbContextOptionsBuilder> optionsAction = (_, opt) =>
        {
            opt.UseSqlServer(connectionString, sql =>
            {
                if (!string.IsNullOrWhiteSpace(migrationsAssembly))
                    sql.MigrationsAssembly(migrationsAssembly);
            });
        };

        generic.Invoke(null, new object?[] { services, optionsAction, null });
        return services;
    }

    public static DbContext CreateDbContext(this IServiceProvider sp, Type dbContextType)
    {
        // IDbContextFactory<TContext>.CreateDbContext() を動的呼び出し
        var factoryType = typeof(IDbContextFactory<>).MakeGenericType(dbContextType);
        var factory = sp.GetRequiredService(factoryType);
        var create = factoryType.GetMethod("CreateDbContext")!;
        return (DbContext)create.Invoke(factory, Array.Empty<object>())!;
    }
}