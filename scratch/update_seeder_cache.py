with open('src/BlazorFluent.Persistence/Initialization/InitialDatabaseSeeder.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target = """        if (changesMade)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Root super-administrators and integrity signatures synchronized successfully.");
        }
    }
}"""

replacement = """        if (changesMade)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Root super-administrators and integrity signatures synchronized successfully.");
        }

        // 4. Startup Cache Warming for high-frequency tenant directory
        var cacheService = scope.ServiceProvider.GetService<ITenantCacheService>();
        if (cacheService != null)
        {
            await cacheService.GetGlobalOrCreateAsync(
                "tenants:all",
                async token =>
                {
                    return (IReadOnlyList<TenantEntity>)await dbContext.Tenants
                        .AsNoTracking()
                        .Where(t => t.Slug != IRootAdminService.RootTenantSlug && t.Slug != "default" && !t.IsDeleted)
                        .OrderBy(t => t.Name)
                        .ToListAsync(token);
                },
                expiration: TimeSpan.FromMinutes(60),
                cancellationToken: cancellationToken);
            logger.LogInformation("Startup cache warming: Tenant directory loaded into HybridCache.");
        }
    }
}"""

norm_text = text.replace('\r\n', '\n')
norm_target = target.replace('\r\n', '\n')
norm_repl = replacement.replace('\r\n', '\n')

if norm_target in norm_text:
    norm_text = norm_text.replace(norm_target, norm_repl, 1)
    if '\r\n' in text:
        norm_text = norm_text.replace('\n', '\r\n')
    with open('src/BlazorFluent.Persistence/Initialization/InitialDatabaseSeeder.cs', 'w', encoding='utf-8') as f:
        f.write(norm_text)
    print('SUCCESS')
else:
    print('NOT FOUND')
