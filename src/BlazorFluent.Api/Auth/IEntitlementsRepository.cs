using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

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

    public InMemoryEntitlementsRepository(IConfiguration? configuration = null)
    {
        // Pre-authorize configured root administrators without hardcoded user records
        if (configuration != null)
        {
            var rawAdmins = configuration["Admin:RootAdmins"];
            var rootTenantId = configuration["Admin:RootTenantId"] ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(rawAdmins))
            {
                var adminEmails = rawAdmins
                    .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(e => e.ToLowerInvariant())
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                foreach (var email in adminEmails)
                {
                    _users[email] = new EntitlementUser
                    {
                        Email = email,
                        TenantId = rootTenantId,
                        StartDate = DateTime.UtcNow.AddMonths(-1),
                        EndDate = DateTime.UtcNow.AddYears(10),
                        IsActive = true,
                        Roles = ["Admin", "SuperAdmin"]
                    };
                }
            }
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
