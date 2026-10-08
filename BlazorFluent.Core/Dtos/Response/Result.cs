namespace BlazorFluent.Core.Dtos.Response;

public class Result
{
    public bool Succeeded { get; protected set; }
    public bool IsSuccess => Succeeded;
    public string? Message { get; protected set; }
    public List<string> Errors { get; protected set; } = [];
    public string? Error => Errors.Count > 0 ? string.Join(", ", Errors) : Message;

    public static Result Success(string? message = null) => new() { Succeeded = true, Message = message };
    public static Result Failure(string error) => new() { Succeeded = false, Errors = [error] };
    public static Result Failure(IEnumerable<string> errors) => new() { Succeeded = false, Errors = errors.ToList() };

    // Fluent aliases for Ok / Fail
    public static Result Ok(string? message = null) => Success(message);
    public static Result<T> Ok<T>(T data, string? message = null) => Result<T>.Success(data, message);
    public static Result Fail(string error) => Failure(error);
    public static Result<T> Fail<T>(string error) => Result<T>.Failure(error);
    public static Result<T> Fail<T>(IEnumerable<string> errors) => Result<T>.Failure(errors);
}

public class Result<T> : Result
{
    public T? Data { get; private set; }
    public T? Value => Data;

    public static Result<T> Success(T data, string? message = null) => new() { Succeeded = true, Data = data, Message = message };
    public static new Result<T> Failure(string error) => new() { Succeeded = false, Errors = [error] };
    public static new Result<T> Failure(IEnumerable<string> errors) => new() { Succeeded = false, Errors = errors.ToList() };

    public static Result<T> Ok(T data, string? message = null) => Success(data, message);
    public static new Result<T> Fail(string error) => Failure(error);
    public static new Result<T> Fail(IEnumerable<string> errors) => Failure(errors);
}
