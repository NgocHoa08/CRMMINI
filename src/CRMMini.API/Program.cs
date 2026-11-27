using CRMMini.API.Extensions;
using CRMMini.Application;
using CRMMini.Application.Interfaces;
using CRMMini.Infrastructure;
using CRMMini.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CRM Mini API v1");
        options.RoutePrefix = "swagger";
    });
}

if (!app.Environment.IsEnvironment("Testing"))
{
    app.UseHttpsRedirection();
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("FrontendCors");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

if (!app.Environment.IsEnvironment("Testing") &&
    builder.Configuration.GetValue("Database:InitializeOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasherService>();

    try
    {
        if (builder.Configuration.GetValue("Database:AutoMigrateOnStartup", false))
            await db.Database.MigrateAsync();

        await DbInitializer.EnsureRolesSeededAsync(db);

        if (builder.Configuration.GetValue("SeedData:Enabled", false))
        {
            var demoPassword = builder.Configuration["SeedData:DefaultDemoPassword"];
            if (!string.IsNullOrWhiteSpace(demoPassword))
            {
                await DbInitializer.SeedDemoDataAsync(db, hasher, demoPassword);
            }
            else
            {
                logger.LogWarning("SeedData:Enabled = true nhưng chưa cấu hình SeedData:DefaultDemoPassword; bỏ qua seed dữ liệu demo.");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Không thể khởi tạo/seed database khi khởi động ứng dụng.");
        throw;
    }
}

app.Run();

public partial class Program;
