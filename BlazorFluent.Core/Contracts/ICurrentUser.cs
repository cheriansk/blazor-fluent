namespace BlazorFluent.Core.Contracts;

public interface ICurrentUser
{
    string Email { get; }
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}

public class DefaultCurrentUser : ICurrentUser
{
    public string Email => "UnAuthUser";
    public string? UserId => "system";
    public string? UserName => "System";
    public bool IsAuthenticated => false;
    public bool IsInRole(string role) => false;
}
