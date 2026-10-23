with open('src/BlazorFluent.Api/Program.cs', 'r', encoding='utf-8') as f:
    text = f.read()

target = """    options.AddPolicy("api-expensive", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));"""

replacement = """    options.AddPolicy("api-expensive", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.Name ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Tenant-Level Partitioned Rate Limiter: prevents noisy neighbor issues across tenants
    options.AddPolicy("api-tenant", httpContext =>
    {
        var tenantId = httpContext.Request.Headers["X-Tenant-Id"].FirstOrDefault()
            ?? httpContext.User.FindFirst("tenant_id")?.Value
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "anonymous-tenant";

        return RateLimitPartition.GetTokenBucketLimiter(
            tenantId,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 200,
                TokensPerPeriod = 50,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                AutoReplenishment = true,
                QueueLimit = 0
            });
    });"""

norm_text = text.replace('\r\n', '\n')
norm_target = target.replace('\r\n', '\n')
norm_repl = replacement.replace('\r\n', '\n')

if norm_target in norm_text:
    norm_text = norm_text.replace(norm_target, norm_repl, 1)
    if '\r\n' in text:
        norm_text = norm_text.replace('\n', '\r\n')
    with open('src/BlazorFluent.Api/Program.cs', 'w', encoding='utf-8') as f:
        f.write(norm_text)
    print('SUCCESS')
else:
    print('NOT FOUND')
