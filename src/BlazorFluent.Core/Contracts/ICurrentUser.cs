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
    void SetSystemDaemon(string daemonName = "SystemDaemon", string serviceEmail = "daemon@system.local");

    /// <summary>
    /// Restores an authenticated user identity into a background execution scope (e.g. queue listeners).
    /// </summary>
    void RestoreUserContext(string userId, string email, string userName, bool isRootAdmin = false);
}

public class DefaultCurrentUser : ICurrentUser
{
    private bool _isSystemDaemon;
    private string? _daemonName;
    private string? _daemonEmail;
    private string? _restoredUserId;
    private string? _restoredEmail;
    private string? _restoredUserName;
    private bool _restoredIsRootAdmin;

    public string Email => _restoredEmail ?? (_isSystemDaemon ? (_daemonEmail ?? "daemon@system.local") : "UnAuthUser");
    public string? UserId => _restoredUserId ?? (_isSystemDaemon ? $"daemon:{_daemonName ?? "SystemDaemon"}" : null);
    public string? UserName => _restoredUserName ?? (_isSystemDaemon ? (_daemonName ?? "SystemDaemon") : "Anonymous");
    public bool IsAuthenticated => _isSystemDaemon || _restoredUserId != null;
    public bool IsInRole(string role) => false;

    public bool IsImpersonated => false;
    public string? ImpersonatedBy => null;
    public string? SessionId => null;
    public bool IsRootAdmin => _restoredIsRootAdmin;
    public bool IsSystemDaemon => _isSystemDaemon;

    public void SetSystemDaemon(string daemonName = "SystemDaemon", string serviceEmail = "daemon@system.local")
    {
        _isSystemDaemon = true;
        _daemonName = daemonName;
        _daemonEmail = serviceEmail;
    }

    public void RestoreUserContext(string userId, string email, string userName, bool isRootAdmin = false)
    {
        _restoredUserId = userId;
        _restoredEmail = email;
        _restoredUserName = userName;
        _restoredIsRootAdmin = isRootAdmin;
    }
}
