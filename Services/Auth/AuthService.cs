using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Models.DTOs;
using QuanLyBanGiay.Models.Entities;

namespace QuanLyBanGiay.Services.Auth;

public sealed class AuthService(
    AppDbContext db,
    IPasswordHasher<User> passwordHasher,
    IConfiguration configuration) : IAuthService
{
    private static readonly HashSet<string> SupportedRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        "Admin", "Staff", "Customer"
    };

    public async Task<AuthOperationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();
        var email = request.Email.Trim();
        if (await db.Users.AnyAsync(user => user.Username.ToLower() == username.ToLower() || user.Email.ToLower() == email.ToLower(), cancellationToken))
            return AuthOperationResult.Failure(409, "Username or email is already registered.");

        var user = new User
        {
            Username = username,
            Email = email,
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            Role = "Customer",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Unique database indexes are the final guard against concurrent duplicate registrations.
            return AuthOperationResult.Failure(409, "Username or email is already registered.");
        }

        return AuthOperationResult.Success(CreateResponse(user), "Customer account registered.", 201);
    }

    public async Task<AuthOperationResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var identifier = request.UsernameOrEmail.Trim();
        var normalizedIdentifier = identifier.ToLower();
        var user = await db.Users.FirstOrDefaultAsync(
            candidate => candidate.Username.ToLower() == normalizedIdentifier || candidate.Email.ToLower() == normalizedIdentifier,
            cancellationToken);

        if (user is null || !user.IsActive || !SupportedRoles.Contains(user.Role))
            return AuthOperationResult.Failure(401, "Invalid username/email or password.");

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return AuthOperationResult.Failure(401, "Invalid username/email or password.");

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }

        return AuthOperationResult.Success(CreateResponse(user), "Signed in successfully.");
    }

    private AuthResponse CreateResponse(User user)
    {
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(60);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim("full_name", user.FullName),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var keyText = configuration["Auth:Jwt:Key"];
        if (string.IsNullOrWhiteSpace(keyText))
            throw new InvalidOperationException("Auth:Jwt:Key must be configured before issuing tokens.");

        var issuer = configuration["Auth:Jwt:Issuer"] ?? "QuanLyBanGiay";
        var audience = configuration["Auth:Jwt:Audience"] ?? "QuanLyBanGiay.Clients";
        var signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyText)),
            SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(issuer, audience, claims, now, expiresAt, signingCredentials);

        return new AuthResponse
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(jwt),
            ExpiresAt = new DateTimeOffset(expiresAt, TimeSpan.Zero),
            User = ToProfile(user)
        };
    }

    public static UserProfileDto ToProfile(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        FullName = user.FullName,
        PhoneNumber = user.Phone,
        Role = user.Role
    };
}
