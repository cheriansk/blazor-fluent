namespace BlazorFluent.Core.Contracts;

public interface ICurrentUser
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    bool IsInRole(string role);
}

public class DefaultCurrentUser : ICurrentUser
{
    public string? UserId => "system";
    public string? UserName => "System";
    public bool IsAuthenticated => true;
    public bool IsInRole(string role) => true;
}
