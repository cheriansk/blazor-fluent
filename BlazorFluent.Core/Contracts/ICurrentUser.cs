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
}

public class DefaultCurrentUser : ICurrentUser
{
    public string Email => "UnAuthUser";
    public string? UserId => "system";
    public string? UserName => "System";
    public bool IsAuthenticated => false;
    public bool IsInRole(string role) => false;

    public bool IsImpersonated => false;
    public string? ImpersonatedBy => null;
    public string? SessionId => null;
}
