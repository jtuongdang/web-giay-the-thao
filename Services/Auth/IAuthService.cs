using QuanLyBanGiay.Models.DTOs;

namespace QuanLyBanGiay.Services.Auth;

public interface IAuthService
{
    Task<AuthOperationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthOperationResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
}

public sealed record AuthOperationResult(bool Succeeded, int StatusCode, string Message, AuthResponse? Response = null)
{
    public static AuthOperationResult Success(AuthResponse response, string message, int statusCode = 200) =>
        new(true, statusCode, message, response);

    public static AuthOperationResult Failure(int statusCode, string message) =>
        new(false, statusCode, message);
}
