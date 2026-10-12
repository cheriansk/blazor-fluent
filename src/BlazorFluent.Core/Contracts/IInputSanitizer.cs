namespace BlazorFluent.Core.Contracts;

public interface IInputSanitizer
{
    /// <summary>
    /// Strips dangerous scripts, events, and malicious HTML vectors while keeping safe formatting tags.
    /// </summary>
    string SanitizeHtml(string rawHtml);

    /// <summary>
    /// Encodes or strips all HTML tags for plain-text string inputs (Title, Name, Slug).
    /// </summary>
    string SanitizePlainText(string rawText);
}
