using BlazorFluent.Core.Contracts;
using Ganss.Xss;

namespace BlazorFluent.Infrastructure.Security;

public class HtmlInputSanitizer : IInputSanitizer
{
    private readonly HtmlSanitizer _sanitizer;

    public HtmlInputSanitizer()
    {
        _sanitizer = new HtmlSanitizer();
    }

    public string SanitizeHtml(string rawHtml)
    {
        return string.IsNullOrWhiteSpace(rawHtml) ? string.Empty : _sanitizer.Sanitize(rawHtml);
    }

    public string SanitizePlainText(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return string.Empty;
        return _sanitizer.Sanitize(rawText).Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
