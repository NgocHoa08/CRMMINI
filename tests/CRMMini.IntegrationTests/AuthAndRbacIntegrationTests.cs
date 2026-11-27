namespace CRMMini.IntegrationTests;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CRMMini.Application.DTOs.Auth;
using CRMMini.Application.DTOs.Users;
using CRMMini.Domain.Constants;
using Microsoft.AspNetCore.Mvc;

public sealed class AuthAndRbacIntegrationTests(CrmWebAppFactory factory) : IClassFixture<CrmWebAppFactory>
{
    [Fact]
    public async Task HealthAndSwaggerEndpoints_ReturnSuccess()
    {
        using var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        var ready = await client.GetAsync("/health/ready");
        var swagger = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401Unauthorized()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Register_Login_Profile_ChangePassword_And_Logout_FlowWorks()
    {
        using var client = factory.CreateClient();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var registerReq = new RegisterRequest(
            $"user_{unique}",
            $"user_{unique}@test.local",
            "PassWord#2026!",
            "New Registered Staff",
            "0909123456");

        var regRes = await client.PostAsJsonAsync("/api/v1/auth/register", registerReq);
        Assert.Equal(HttpStatusCode.Created, regRes.StatusCode);
        var auth = await regRes.Content.ReadFromJsonAsync<AuthResponse>(CrmWebAppFactory.JsonOptions);
        Assert.NotNull(auth);
        Assert.Contains(SystemRoles.Staff, auth!.User.Roles);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var meRes = await client.GetAsync("/api/v1/users/me");
        Assert.Equal(HttpStatusCode.OK, meRes.StatusCode);

        // Staff cannot access admin-only create user
        var createUserRes = await client.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            $"blocked_{unique}", $"blocked_{unique}@test.local", "PassWord#2026!", "Blocked", null, null, [SystemRoles.Staff]));
        Assert.Equal(HttpStatusCode.Forbidden, createUserRes.StatusCode);

        // Change password -> revokes old refresh token
        var changePwRes = await client.PutAsJsonAsync("/api/v1/users/me/password",
            new ChangePasswordRequest("PassWord#2026!", "NewPass#2026!"));
        Assert.Equal(HttpStatusCode.NoContent, changePwRes.StatusCode);

        var refreshAfterPw = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(auth.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterPw.StatusCode);

        // Login with new password succeeds
        var loginRes = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(registerReq.Email, "NewPass#2026!"));
        Assert.Equal(HttpStatusCode.OK, loginRes.StatusCode);
        var loggedIn = await loginRes.Content.ReadFromJsonAsync<AuthResponse>(CrmWebAppFactory.JsonOptions);

        // Logout revokes refresh token
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loggedIn!.AccessToken);
        var logoutRes = await client.PostAsJsonAsync("/api/v1/auth/logout", new LogoutRequest(loggedIn.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        var refreshAfterLogout = await client.PostAsJsonAsync("/api/v1/auth/refresh",
            new RefreshTokenRequest(loggedIn.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogout.StatusCode);
    }

    [Fact]
    public async Task Login_InvalidPasswordAndAdminLockedAccount_ReturnsProblemDetailsAndAuditBlocks()
    {
        using var adminClient = await factory.CreateAuthenticatedClientAsync("admin@test.local");
        var unique = Guid.NewGuid().ToString("N")[..8];

        var createRes = await adminClient.PostAsJsonAsync("/api/v1/users", new CreateUserRequest(
            $"lock_{unique}",
            $"lock_{unique}@test.local",
            "PassWord#2026!",
            "Lock Test User",
            null,
            factory.TeamAId,
            [SystemRoles.Staff]));
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        var created = (await createRes.Content.ReadFromJsonAsync<UserProfileDto>(CrmWebAppFactory.JsonOptions))!;

        using var anonClient = factory.CreateClient();

        // Wrong password -> 401 ProblemDetails
        var badLogin = await anonClient.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(created.Email, "WrongPassword#1"));
        Assert.Equal(HttpStatusCode.Unauthorized, badLogin.StatusCode);
        var problem = await badLogin.Content.ReadFromJsonAsync<ProblemDetails>(CrmWebAppFactory.JsonOptions);
        Assert.Equal(401, problem!.Status);

        // Admin locks account -> login immediately returns 403 Forbidden
        var lockRes = await adminClient.PutAsJsonAsync($"/api/v1/users/{created.Id}",
            new UpdateUserRequest(created.FullName, created.Phone, created.TeamId, true, true, [SystemRoles.Staff]));
        Assert.Equal(HttpStatusCode.OK, lockRes.StatusCode);

        var lockedLogin = await anonClient.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(created.Email, "PassWord#2026!"));
        Assert.Equal(HttpStatusCode.Forbidden, lockedLogin.StatusCode);
    }
}
