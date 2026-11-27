namespace CRMMini.UnitTests;

using CRMMini.Application.Common;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.Interfaces;
using CRMMini.Application.Services;
using CRMMini.Application.Validators;
using CRMMini.Domain.Constants;
using CRMMini.Domain.Entities;
using CRMMini.Domain.Exceptions;
using CRMMini.Infrastructure.Persistence;
using CRMMini.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class AuthServiceUnitTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private CrmDbContext _db = null!;
    private FakeClock _clock = null!;
    private FakeCurrentUserService _currentUser = null!;
    private AuthService _authService = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _clock = new FakeClock(new DateTimeOffset(2026, 11, 27, 8, 0, 0, TimeSpan.Zero));
        _currentUser = new FakeCurrentUserService();

        var options = new DbContextOptionsBuilder<CrmDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new CrmDbContext(options, _currentUser, _clock);
        await _db.Database.EnsureCreatedAsync();
        await DbInitializer.SeedRolesAsync(_db);

        var jwtOptions = Options.Create(new JwtSettings
        {
            SecretKey = "unit-test-super-secret-signing-key-32chars!",
            Issuer = "CRMMini.Tests",
            Audience = "CRMMini.Tests",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        });
        var securityOptions = Options.Create(new SecuritySettings
        {
            MaxFailedLoginAttempts = 3,
            LockoutMinutes = 15
        });

        _authService = new AuthService(
            _db,
            new PasswordHasherService(),
            new JwtTokenService(jwtOptions, _clock),
            _currentUser,
            new AuditService(_db, _currentUser, _clock),
            _clock,
            jwtOptions,
            securityOptions,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            new RefreshTokenRequestValidator(),
            new LogoutRequestValidator());
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Login_LocksAccountAfterMaxFailedAttempts_AndUnlocksAfterCooldown()
    {
        await _authService.RegisterAsync(new RegisterRequest(
            "testuser", "test@example.com", "ValidPass#123", "Test User", null));

        // 2 failed attempts -> UnauthorizedException
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _authService.LoginAsync(new LoginRequest("test@example.com", "WrongPass#1")));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _authService.LoginAsync(new LoginRequest("test@example.com", "WrongPass#2")));

        // 3rd failed attempt -> locks account and throws ForbiddenException
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _authService.LoginAsync(new LoginRequest("test@example.com", "WrongPass#3")));

        // Even valid password fails while locked
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _authService.LoginAsync(new LoginRequest("test@example.com", "ValidPass#123")));

        // Advance past lockout cooldown -> succeeds
        _clock.UtcNow = _clock.UtcNow.AddMinutes(16);
        var auth = await _authService.LoginAsync(new LoginRequest("test@example.com", "ValidPass#123"));

        Assert.False(string.IsNullOrWhiteSpace(auth.AccessToken));
        Assert.Equal("testuser", auth.User.UserName);
    }

    [Fact]
    public async Task RefreshToken_ReuseRevokedToken_RevokesEntireTokenFamily()
    {
        var initial = await _authService.RegisterAsync(new RegisterRequest(
            "rotuser", "rot@example.com", "ValidPass#123", "Rotate User", null));

        var rotated = await _authService.RefreshTokenAsync(new RefreshTokenRequest(initial.RefreshToken));
        Assert.NotEqual(initial.RefreshToken, rotated.RefreshToken);

        // Reuse the old refresh token -> rejected AND revokes rotated token too
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _authService.RefreshTokenAsync(new RefreshTokenRequest(initial.RefreshToken)));

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _authService.RefreshTokenAsync(new RefreshTokenRequest(rotated.RefreshToken)));
    }

    private sealed class FakeClock(DateTimeOffset initial) : IDateTimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = initial;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public CurrentUserInfo? CurrentUser { get; set; }
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "xUnit";
        public CurrentUserInfo GetRequiredUser() => CurrentUser ?? throw new UnauthorizedException();
    }
}
