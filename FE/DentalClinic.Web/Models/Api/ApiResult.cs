namespace DentalClinic.Web.Models.Api;

public class ApiResult<T>
{
    public bool IsSuccess { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public List<string> Errors { get; set; } = new();
    public int StatusCode { get; set; }
    public bool IsUnverified { get; set; }
    public bool IsLocked { get; set; }

    public static ApiResult<T> Success(T? data, string? message = null, int statusCode = 200) =>
        new() { IsSuccess = true, Data = data, Message = message, StatusCode = statusCode };

    public static ApiResult<T> Failure(string message, List<string>? errors = null, int statusCode = 400, bool isUnverified = false, bool isLocked = false) =>
        new()
        {
            IsSuccess = false,
            Message = message,
            Errors = errors ?? (string.IsNullOrEmpty(message) ? new List<string>() : new List<string> { message }),
            StatusCode = statusCode,
            IsUnverified = isUnverified,
            IsLocked = isLocked
        };
}

public class ApiResult : ApiResult<object>
{
    public static ApiResult Success(string? message = null, int statusCode = 200) =>
        new() { IsSuccess = true, Message = message, StatusCode = statusCode };

    public static new ApiResult Failure(string message, List<string>? errors = null, int statusCode = 400, bool isUnverified = false, bool isLocked = false) =>
        new()
        {
            IsSuccess = false,
            Message = message,
            Errors = errors ?? (string.IsNullOrEmpty(message) ? new List<string>() : new List<string> { message }),
            StatusCode = statusCode,
            IsUnverified = isUnverified,
            IsLocked = isLocked
        };
}
