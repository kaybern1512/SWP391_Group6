using System.Collections.Generic;

namespace DentalClinic.Application.Common.Models;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public string? ErrorCode { get; set; }

    public static ApiResponse<T> Ok(T? data, string? message = "Thao tác thành công.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data,
            Errors = null,
            ErrorCode = null
        };
    }

    public static ApiResponse<T> Fail(string message, List<string>? errors = null, string? errorCode = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Data = default,
            ErrorCode = errorCode,
            Errors = errors ?? new List<string> { message }
        };
    }

    public static ApiResponse<T> Forbidden(string message, string? errorCode = "FORBIDDEN")
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Data = default,
            ErrorCode = errorCode ?? "FORBIDDEN",
            Errors = new List<string> { message }
        };
    }
}

public class ApiResponse : ApiResponse<object>
{
    public new static ApiResponse Ok(object? data = null, string? message = "Thao tác thành công.")
    {
        return new ApiResponse
        {
            Success = true,
            Message = message,
            Data = data,
            Errors = null,
            ErrorCode = null
        };
    }

    public new static ApiResponse Fail(string message, List<string>? errors = null, string? errorCode = null)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message,
            Data = null,
            ErrorCode = errorCode,
            Errors = errors ?? new List<string> { message }
        };
    }

    public new static ApiResponse Forbidden(string message, string? errorCode = "FORBIDDEN")
    {
        return new ApiResponse
        {
            Success = false,
            Message = message,
            Data = null,
            ErrorCode = errorCode ?? "FORBIDDEN",
            Errors = new List<string> { message }
        };
    }
}
