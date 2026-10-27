namespace BlazorEnterprise.Core.Configuration;

/// <summary>
/// Operational profile for Content-Security-Policy header generation.
/// </summary>
public enum CspProfile
{
    /// <summary>
    /// Eliminates 'unsafe-inline' and 'unsafe-eval' completely from both script-src and style-src.
    /// Employs per-request 128-bit cryptographic nonces. Recommended for strict enterprise InfoSec audits.
    /// </summary>
    PureStrict,

    /// <summary>
    /// Eliminates 'unsafe-inline' and 'unsafe-eval' from script-src using per-request nonces ('strict-dynamic').
    /// Enforces nonces on stylesheet elements (style-src-elem) while permitting element inline style attributes (style-src-attr 'unsafe-inline').
    /// </summary>
    Level3Isolated
}

/// <summary>
/// Strongly typed configuration options for Content-Security-Policy (CSP) headers.
/// Section: "Security:Csp"
/// </summary>
public class CspOptions
{
    public const string SectionName = "Security:Csp";

    /// <summary>
    /// Whether CSP headers should be emitted. Default is true.
    /// </summary>
    public bool Enforce { get; set; } = true;

    /// <summary>
    /// If true, emits 'Content-Security-Policy-Report-Only' instead of 'Content-Security-Policy'.
    /// </summary>
    public bool ReportOnly { get; set; } = false;

    /// <summary>
    /// Optional violation report endpoint URI (report-uri / report-to).
    /// </summary>
    public string? ReportUri { get; set; } = "/api/security/csp-violations";

    /// <summary>
    /// The strictness profile applied to script and style directives. Default is PureStrict.
    /// </summary>
    public CspProfile Profile { get; set; } = CspProfile.PureStrict;
}
