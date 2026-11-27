namespace CRMMini.API.Extensions;

using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using CRMMini.API.Middleware;
using CRMMini.API.Services;
using CRMMini.Application.Common;
using CRMMini.Application.Interfaces;
using CRMMini.Domain.Constants;
using CRMMini.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.Configure<SecuritySettings>(configuration.GetSection(SecuritySettings.SectionName));

        var jwtSection = configuration.GetSection(JwtSettings.SectionName);
        var jwtSettings = jwtSection.Get<JwtSettings>() ?? new JwtSettings();
        jwtSettings.Issuer = string.IsNullOrWhiteSpace(jwtSettings.Issuer) ? "CRMMini" : jwtSettings.Issuer;
        jwtSettings.Audience = string.IsNullOrWhiteSpace(jwtSettings.Audience) ? "CRMMini.Client" : jwtSettings.Audience;
        jwtSettings.AccessTokenExpirationMinutes = jwtSettings.AccessTokenExpirationMinutes > 0 ? jwtSettings.AccessTokenExpirationMinutes : 15;
        jwtSettings.RefreshTokenExpirationDays = jwtSettings.RefreshTokenExpirationDays > 0 ? jwtSettings.RefreshTokenExpirationDays : 7;

        if (string.IsNullOrWhiteSpace(jwtSettings.SecretKey) || jwtSettings.SecretKey.Length < 32)
        {
            if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            {
                // Ephemeral dev-only fallback key so no signing secret is ever committed in source control.
                jwtSettings.SecretKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            }
            else
            {
                throw new InvalidOperationException(
                    "JwtSettings:SecretKey phải được cấu hình qua biến môi trường hoặc secret manager với tối thiểu 32 ký tự.");
            }
        }

        services.Configure<JwtSettings>(options =>
        {
            options.SecretKey = jwtSettings.SecretKey;
            options.Issuer = jwtSettings.Issuer;
            options.Audience = jwtSettings.Audience;
            options.AccessTokenExpirationMinutes = jwtSettings.AccessTokenExpirationMinutes;
            options.RefreshTokenExpirationDays = jwtSettings.RefreshTokenExpirationDays;
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", p => p.RequireRole(SystemRoles.Admin))
            .AddPolicy("ManagerOrAdmin", p => p.RequireRole(SystemRoles.Admin, SystemRoles.Manager))
            .AddPolicy("StaffOrAbove", p => p.RequireRole(SystemRoles.Admin, SystemRoles.Manager, SystemRoles.Staff));

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        services.AddProblemDetails();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        })
        .AddMvc()
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "CRM Mini Backend API",
                Version = "v1",
                Description = "API quản lý quan hệ khách hàng CRM Mini (.NET 10, EF Core 10, SQL Server)."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Nhập JWT access token (Swagger tự thêm tiền tố Bearer)."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
                options.IncludeXmlComments(xmlPath);
        });

        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 30);
        var apiPermitLimit = configuration.GetValue("RateLimiting:ApiPermitLimit", 300);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, token) =>
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Bạn đã gửi quá nhiều yêu cầu trong thời gian ngắn. Vui lòng thử lại sau.",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["errorCode"] = "rate_limit_exceeded";
                problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(problem, token);
            };

            options.AddFixedWindowLimiter("auth", opt =>
            {
                opt.PermitLimit = authPermitLimit;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });

            options.AddFixedWindowLimiter("api", opt =>
            {
                opt.PermitLimit = apiPermitLimit;
                opt.Window = TimeSpan.FromMinutes(1);
                opt.QueueLimit = 0;
            });
        });

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        services.AddCors(options =>
        {
            options.AddPolicy("FrontendCors", policy =>
            {
                if (allowedOrigins.Length > 0)
                    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
                else
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            });
        });

        services.AddHealthChecks()
            .AddDbContextCheck<CrmDbContext>("database", tags: ["ready"]);

        return services;
    }
}
