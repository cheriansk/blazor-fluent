namespace BlazorFluent.Core.Common;

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
}

public class Result<T> : Result
{
    public T? Data { get; private set; }

    public static Result<T> Success(T data, string? message = null) => new() { Succeeded = true, Data = data, Message = message };
    public static new Result<T> Failure(string error) => new() { Succeeded = false, Errors = [error] };
    public static new Result<T> Failure(IEnumerable<string> errors) => new() { Succeeded = false, Errors = errors.ToList() };
}
