namespace BlazorFluent.Core.Contracts;

public interface ICurrentUser
{
    string Email { get; }
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);

    // Impersonation & Session Tracking
    bool IsImpersonated { get; }
    string? ImpersonatedBy { get; }
    string? SessionId { get; }

    /// <summary>
    /// Indicates whether the current authenticated user is one of the designated root super-administrators.
    /// Evaluated on-demand with no mutable setter.
    /// </summary>
    bool IsRootAdmin { get; }

    /// <summary>
    /// Indicates whether the current execution context is an internal background daemon / service worker.
    /// </summary>
    bool IsSystemDaemon { get; }

    /// <summary>
    /// Configures the current scoped context as an internal background system daemon.
    /// Strictly restricted to background workers and seeders.
    /// </summary>
    void SetSystemDaemon(string daemonName = "SystemDaemon");
}

public class DefaultCurrentUser : ICurrentUser
{
    private bool _isSystemDaemon;
    private string? _daemonName;

    public string Email => _isSystemDaemon ? "system@daemon.local" : "UnAuthUser";
    public string? UserId => _isSystemDaemon ? "system" : null;
    public string? UserName => _isSystemDaemon ? (_daemonName ?? "SystemDaemon") : "Anonymous";
    public bool IsAuthenticated => _isSystemDaemon;
    public bool IsInRole(string role) => false;

    public bool IsImpersonated => false;
    public string? ImpersonatedBy => null;
    public string? SessionId => null;
    public bool IsRootAdmin => false;
    public bool IsSystemDaemon => _isSystemDaemon;

    public void SetSystemDaemon(string daemonName = "SystemDaemon")
    {
        _isSystemDaemon = true;
        _daemonName = daemonName;
    }
}
