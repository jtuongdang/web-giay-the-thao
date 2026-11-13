namespace QuanLyBanGiay.Models.DTOs;

/// <summary>Credentials used to sign in with a username or email address.</summary>
public sealed record LoginRequest
{
    /// <summary>Account username or email address.</summary>
    public string UsernameOrEmail { get; init; } = string.Empty;

    /// <summary>Account password.</summary>
    public string Password { get; init; } = string.Empty;
}

/// <summary>Customer account information used to create an account.</summary>
public sealed record RegisterRequest
{
    /// <summary>Customer's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Account username.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>Customer's email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Customer's phone number.</summary>
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>Password for the new account.</summary>
    public string Password { get; init; } = string.Empty;
}

/// <summary>Authentication token and signed-in user information.</summary>
public sealed record AuthResponse
{
    /// <summary>Bearer access token.</summary>
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>Token expiration time in UTC.</summary>
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Signed-in user's profile.</summary>
    public UserProfileDto User { get; init; } = new();
}

/// <summary>Public profile information for the current user.</summary>
public sealed record UserProfileDto
{
    /// <summary>User identifier.</summary>
    public int Id { get; init; }

    /// <summary>Account username.</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>User's email address.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>User's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>User's phone number.</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>Assigned authorization role, such as Customer, Staff, or Admin.</summary>
    public string Role { get; init; } = string.Empty;
}
