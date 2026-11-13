namespace QuanLyBanGiay.Models.DTOs;

/// <summary>Standard envelope returned by the REST API.</summary>
/// <typeparam name="T">The type of the response payload.</typeparam>
public sealed record ApiResponse<T>
{
    /// <summary>Whether the operation completed successfully.</summary>
    public bool Success { get; init; }

    /// <summary>HTTP status code associated with the response.</summary>
    public int StatusCode { get; init; }

    /// <summary>Human-readable result message.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Response payload, or <see langword="null"/> when unavailable.</summary>
    public T? Data { get; init; }

    /// <summary>Validation or operation errors, if any.</summary>
    public IReadOnlyList<string>? Errors { get; init; }

    /// <summary>UTC time at which the response was created.</summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Creates a successful response.</summary>
    /// <param name="data">Response payload.</param>
    /// <param name="message">Human-readable success message.</param>
    /// <param name="statusCode">HTTP status code.</param>
    public static ApiResponse<T> Ok(
        T? data,
        string message = "Request completed successfully",
        int statusCode = 200) =>
        new()
        {
            Success = true,
            StatusCode = statusCode,
            Message = message,
            Data = data
        };

    /// <summary>Creates a failed response.</summary>
    /// <param name="message">Human-readable failure message.</param>
    /// <param name="statusCode">HTTP status code.</param>
    /// <param name="errors">Optional error details.</param>
    public static ApiResponse<T> Fail(
        string message,
        int statusCode = 400,
        IReadOnlyList<string>? errors = null) =>
        new()
        {
            Success = false,
            StatusCode = statusCode,
            Message = message,
            Errors = errors
        };
}
