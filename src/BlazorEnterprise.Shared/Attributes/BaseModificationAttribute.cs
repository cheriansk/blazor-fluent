namespace BlazorEnterprise.Shared.Attributes;

/// <summary>
/// Mandatory attribute applied to any base product method, property, or class
/// that has been intentionally modified from its upstream implementation.
/// </summary>
[AttributeUsage(
    AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Interface, 
    Inherited = false, 
    AllowMultiple = true)]
public sealed class BaseModificationAttribute : Attribute
{
    /// <summary>Developer who made the modification.</summary>
    public string Author { get; }

    /// <summary>Date of modification in YYYY-MM-DD format.</summary>
    public string Date { get; }

    /// <summary>Tracking ticket, Jira ID, or work item reference.</summary>
    public string Ticket { get; }

    /// <summary>Clear rationale explaining why the base behavior was changed.</summary>
    public string Reason { get; }

    /// <summary>Summary of what the original base behavior was prior to modification.</summary>
    public string OriginalBehavior { get; }

    public BaseModificationAttribute(
        string author, 
        string date, 
        string ticket, 
        string reason, 
        string originalBehavior)
    {
        Author = author;
        Date = date;
        Ticket = ticket;
        Reason = reason;
        OriginalBehavior = originalBehavior;
    }
}
