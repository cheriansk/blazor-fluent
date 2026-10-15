using System.Collections.Concurrent;

namespace BlazorFluent.Api.Auth;

public interface IEntitlementsRepository
{
    Task<EntitlementUser?> GetByEmailAsync(string email);
    Task<IReadOnlyList<EntitlementUser>> GetAllAsync();
    Task UpsertAsync(EntitlementUser user);
    Task<bool> DeleteAsync(string email);
}

public class InMemoryEntitlementsRepository : IEntitlementsRepository
{
    private readonly ConcurrentDictionary<string, EntitlementUser> _users = new(StringComparer.OrdinalIgnoreCase);

    public InMemoryEntitlementsRepository()
    {
        // Seed default developer and client pilot entitlements
        var seedUsers = new[]
        {
            new EntitlementUser
            {
                Email = "admin@company.com",
                TenantId = "TENANT-ROOT-001",
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddYears(5),
                IsActive = true,
                Roles = ["Admin", "SuperAdmin"]
            },
            new EntitlementUser
            {
                Email = "analyst@clientcorp.com",
                TenantId = "TENANT-CLIENT-002",
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddYears(1),
                IsActive = true,
                Roles = ["User", "ProjectManager"]
            }
        };

        foreach (var u in seedUsers)
        {
            _users[u.Email] = u;
        }
    }

    public Task<EntitlementUser?> GetByEmailAsync(string email)
    {
        _users.TryGetValue(email, out var user);
        return Task.FromResult(user);
    }

    public Task<IReadOnlyList<EntitlementUser>> GetAllAsync()
    {
        IReadOnlyList<EntitlementUser> list = _users.Values.OrderBy(u => u.Email).ToList();
        return Task.FromResult(list);
    }

    public Task UpsertAsync(EntitlementUser user)
    {
        user.UpdatedAtUtc = DateTime.UtcNow;
        _users[user.Email] = user;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(string email)
    {
        var removed = _users.TryRemove(email, out _);
        return Task.FromResult(removed);
    }
}
