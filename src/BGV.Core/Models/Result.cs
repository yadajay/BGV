namespace BGV.Core.Models;

public class Result
{
    public bool Success { get; set; }
    public string? Error { get; set; }

    public static Result Ok() => new() { Success = true };
    public static Result Fail(string error) => new() { Success = false, Error = error };
}

public class Result<T> : Result
{
    public T? Data { get; set; }

    public static Result<T> Ok(T data) => new() { Success = true, Data = data };
    public static new Result<T> Fail(string error) => new() { Success = false, Error = error };
}