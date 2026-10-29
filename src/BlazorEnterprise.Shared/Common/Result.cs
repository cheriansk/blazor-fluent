using System.IO;
using System.Runtime.CompilerServices;

namespace BlazorEnterprise.Shared.Common;

/// <summary>
/// Functional Result pattern representing the success or failure outcome of an operation.
/// </summary>
public class Result
{
    public bool Succeeded { get; protected set; }
    public bool IsSuccess => Succeeded;
    public string? Message { get; protected set; }
    public List<string> Errors { get; protected set; } = [];
    public string? Error => Errors.Count > 0 ? string.Join(", ", Errors) : Message;

    /// <summary>
    /// Exact source code origin where this failure was created (e.g. "TenantService.cs:388 [CreateUserAsync]").
    /// Automatically captured by the compiler via CallerFilePath, CallerLineNumber, and CallerMemberName.
    /// </summary>
    public string? DiagnosticOrigin { get; protected set; }

    public static Result Success(string? message = null) => new() { Succeeded = true, Message = message };

    public static Result Failure(
        string error,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
    {
        var origin = FormatOrigin(filePath, lineNumber, memberName);
        var displayError = FormatErrorWithOrigin(error, origin);
        return new Result
        {
            Succeeded = false,
            Errors = [displayError],
            DiagnosticOrigin = origin
        };
    }

    public static Result Failure(
        IEnumerable<string> errors,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
    {
        var origin = FormatOrigin(filePath, lineNumber, memberName);
        var displayErrors = errors.Select(e => FormatErrorWithOrigin(e, origin)).ToList();
        return new Result
        {
            Succeeded = false,
            Errors = displayErrors,
            DiagnosticOrigin = origin
        };
    }

    // Fluent aliases for Ok / Fail
    public static Result Ok(string? message = null) => Success(message);
    public static Result<T> Ok<T>(T data, string? message = null) => Result<T>.Success(data, message);

    public static Result Fail(
        string error,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") => Failure(error, filePath, lineNumber, memberName);

    public static Result Fail(
        IEnumerable<string> errors,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") => Failure(errors, filePath, lineNumber, memberName);

    public static Result<T> Fail<T>(
        string error,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") => Result<T>.Failure(error, filePath, lineNumber, memberName);

    public static Result<T> Fail<T>(
        IEnumerable<string> errors,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") => Result<T>.Failure(errors, filePath, lineNumber, memberName);

    internal static string? FormatOrigin(string filePath, int lineNumber, string memberName)
    {
        var fileName = Path.GetFileName(filePath);
        return !string.IsNullOrEmpty(fileName) ? $"{fileName}:{lineNumber} [{memberName}]" : null;
    }

    internal static string FormatErrorWithOrigin(string error, string? origin)
    {
#if DEBUG
        if (!string.IsNullOrEmpty(origin) && !error.Contains(origin, StringComparison.Ordinal))
        {
            return $"{error} ({origin})";
        }
#endif
        return error;
    }
}

/// <summary>
/// Functional Result pattern containing a strongly-typed data payload.
/// </summary>
public class Result<T> : Result
{
    public T? Data { get; private set; }
    public T? Value => Data;

    public static Result<T> Success(T data, string? message = null) => new() { Succeeded = true, Data = data, Message = message };

    public static new Result<T> Failure(
        string error,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
    {
        var origin = FormatOrigin(filePath, lineNumber, memberName);
        var displayError = FormatErrorWithOrigin(error, origin);
        return new Result<T>
        {
            Succeeded = false,
            Errors = [displayError],
            DiagnosticOrigin = origin
        };
    }

    public static new Result<T> Failure(
        IEnumerable<string> errors,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
    {
        var origin = FormatOrigin(filePath, lineNumber, memberName);
        var displayErrors = errors.Select(e => FormatErrorWithOrigin(e, origin)).ToList();
        return new Result<T>
        {
            Succeeded = false,
            Errors = displayErrors,
            DiagnosticOrigin = origin
        };
    }

    public static Result<T> Ok(T data, string? message = null) => Success(data, message);

    public static new Result<T> Fail(
        string error,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") => Failure(error, filePath, lineNumber, memberName);

    public static new Result<T> Fail(
        IEnumerable<string> errors,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "") => Failure(errors, filePath, lineNumber, memberName);
}
