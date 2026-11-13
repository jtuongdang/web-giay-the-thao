using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using QuanLyBanGiay.Data;
using QuanLyBanGiay.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<QuanLyBanGiay.Models.Entities.User>, Microsoft.AspNetCore.Identity.PasswordHasher<QuanLyBanGiay.Models.Entities.User>>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

var configuredJwtKey = builder.Configuration["Auth:Jwt:Key"];
if (string.IsNullOrWhiteSpace(configuredJwtKey) && !builder.Environment.IsDevelopment())
    throw new InvalidOperationException("Configure Auth:Jwt:Key with a randomly generated secret of at least 32 bytes.");

// A process-local key keeps local development convenient. Production must use a managed secret.
var jwtKey = string.IsNullOrWhiteSpace(configuredJwtKey)
    ? Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64))
    : configuredJwtKey;
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Auth:Jwt:Key must contain at least 32 bytes.");
builder.Configuration["Auth:Jwt:Key"] = jwtKey;
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
var jwtIssuer = builder.Configuration["Auth:Jwt:Issuer"] ?? "QuanLyBanGiay";
var jwtAudience = builder.Configuration["Auth:Jwt:Audience"] ?? "QuanLyBanGiay.Clients";

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = "BearerOrCookie";
        options.DefaultAuthenticateScheme = "BearerOrCookie";
        options.DefaultChallengeScheme = "BearerOrCookie";
    })
    .AddPolicyScheme("BearerOrCookie", "JWT bearer or Admin Portal cookie", options =>
    {
        options.ForwardDefaultSelector = context =>
            context.Request.Headers.Authorization.Any(value => value?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
                ? JwtBearerDefaults.AuthenticationScheme
                : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "QuanLyBanGiay.AdminAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.SlidingExpiration = true;
        options.Events.OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.SeedAsync(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
