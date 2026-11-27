namespace CRMMini.API.Middleware;

using CRMMini.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IHostEnvironment environment) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        if (exception is AppException appEx)
        {
            logger.LogWarning(
                "Handled application exception {ErrorCode} ({StatusCode}) on {Method} {Path}. TraceId={TraceId}: {Message}",
                appEx.ErrorCode,
                appEx.StatusCode,
                httpContext.Request.Method,
                httpContext.Request.Path,
                traceId,
                appEx.Message);

            ProblemDetails problem = appEx.Errors is not null
                ? new ValidationProblemDetails(new Dictionary<string, string[]>(appEx.Errors))
                {
                    Status = appEx.StatusCode,
                    Title = GetTitle(appEx.StatusCode),
                    Detail = appEx.Message,
                    Instance = httpContext.Request.Path
                }
                : new ProblemDetails
                {
                    Status = appEx.StatusCode,
                    Title = GetTitle(appEx.StatusCode),
                    Detail = appEx.Message,
                    Instance = httpContext.Request.Path
                };

            problem.Extensions["errorCode"] = appEx.ErrorCode;
            problem.Extensions["traceId"] = traceId;

            httpContext.Response.StatusCode = appEx.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken);
            return true;
        }

        logger.LogError(
            exception,
            "Unhandled exception on {Method} {Path}. TraceId={TraceId}",
            httpContext.Request.Method,
            httpContext.Request.Path,
            traceId);

        var serverError = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = environment.IsDevelopment()
                ? exception.Message
                : "Đã xảy ra lỗi hệ thống. Vui lòng thử lại sau hoặc cung cấp traceId cho bộ phận hỗ trợ.",
            Instance = httpContext.Request.Path
        };
        serverError.Extensions["errorCode"] = "internal_server_error";
        serverError.Extensions["traceId"] = traceId;

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(serverError, cancellationToken);
        return true;
    }

    private static string GetTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        422 => "Unprocessable Entity",
        _ => "Application Error"
    };
}
