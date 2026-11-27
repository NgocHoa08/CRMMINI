namespace CRMMini.IntegrationTests;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.Interfaces;
using CRMMini.Domain.Constants;
using CRMMini.Domain.Entities;
using CRMMini.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class CrmWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _sqliteConnection = new("DataSource=:memory:");

    public const string DemoPassword = "TestPassword#2026!";

    public Guid TeamAId { get; private set; }
    public Guid TeamBId { get; private set; }
    public Guid AdminId { get; private set; }
    public Guid ManagerAId { get; private set; }
    public Guid StaffAId { get; private set; }
    public Guid StaffBId { get; private set; }

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:SecretKey"] = "integration-test-jwt-secret-key-min-32-chars!!",
                ["JwtSettings:Issuer"] = "CRMMini.Tests",
                ["JwtSettings:Audience"] = "CRMMini.Tests",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "15",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7",
                ["SecuritySettings:MaxFailedLoginAttempts"] = "3",
                ["SecuritySettings:LockoutMinutes"] = "15",
                ["RateLimiting:AuthPermitLimit"] = "500",
                ["RateLimiting:ApiPermitLimit"] = "2000",
                ["Swagger:Enabled"] = "true",
                ["Database:InitializeOnStartup"] = "false",
                ["SeedData:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDbContextOptionsConfiguration<CrmDbContext>>();
            services.RemoveAll<DbContextOptions<CrmDbContext>>();
            services.RemoveAll<CrmDbContext>();

            services.AddDbContext<CrmDbContext>(options =>
                options.UseSqlite(_sqliteConnection));
        });
    }

    public async Task InitializeAsync()
    {
        await _sqliteConnection.OpenAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

        await db.Database.EnsureCreatedAsync();
        await DbInitializer.SeedRolesAsync(db);

        var roles = await db.Roles.ToDictionaryAsync(r => r.Name, StringComparer.OrdinalIgnoreCase);
        var now = DateTimeOffset.UtcNow;

        var teamA = new Team { Name = "Team Alpha", Code = "TEAM-A", IsActive = true, CreatedAt = now };
        var teamB = new Team { Name = "Team Beta", Code = "TEAM-B", IsActive = true, CreatedAt = now };
        db.Teams.AddRange(teamA, teamB);
        await db.SaveChangesAsync();

        var admin = BuildUser("admin.test", "admin@test.local", "Admin Test", null, roles[SystemRoles.Admin], hasher, now);
        var managerA = BuildUser("mgr.a", "mgra@test.local", "Manager Alpha", teamA.Id, roles[SystemRoles.Manager], hasher, now);
        var staffA = BuildUser("staff.a", "staffa@test.local", "Staff Alpha", teamA.Id, roles[SystemRoles.Staff], hasher, now);
        var staffB = BuildUser("staff.b", "staffb@test.local", "Staff Beta", teamB.Id, roles[SystemRoles.Staff], hasher, now);

        db.Users.AddRange(admin, managerA, staffA, staffB);
        await db.SaveChangesAsync();

        teamA.ManagerUserId = managerA.Id;
        await db.SaveChangesAsync();

        TeamAId = teamA.Id;
        TeamBId = teamB.Id;
        AdminId = admin.Id;
        ManagerAId = managerA.Id;
        StaffAId = staffA.Id;
        StaffBId = staffB.Id;
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _sqliteConnection.DisposeAsync();
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(string emailOrUserName, string password = DemoPassword)
    {
        var client = CreateClient();
        var auth = await LoginAsync(client, emailOrUserName, password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    public static async Task<AuthResponse> LoginAsync(HttpClient client, string emailOrUserName, string password = DemoPassword)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(emailOrUserName, password));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions))!;
    }

    private static User BuildUser(
        string userName,
        string email,
        string fullName,
        Guid? teamId,
        Role role,
        IPasswordHasherService hasher,
        DateTimeOffset now)
    {
        var user = new User
        {
            UserName = userName,
            Email = email,
            FullName = fullName,
            TeamId = teamId,
            IsActive = true,
            IsLocked = false,
            CreatedAt = now
        };
        user.PasswordHash = hasher.HashPassword(user, DemoPassword);
        user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id, AssignedAt = now });
        return user;
    }
}
