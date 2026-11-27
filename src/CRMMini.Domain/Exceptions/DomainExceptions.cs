namespace CRMMini.Domain.Exceptions;

public abstract class AppException(
    string message,
    int statusCode,
    string errorCode,
    IDictionary<string, string[]>? errors = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string ErrorCode { get; } = errorCode;
    public IDictionary<string, string[]>? Errors { get; } = errors;
}

public sealed class NotFoundException(string message)
    : AppException(message, 404, "not_found");

public sealed class ConflictException(string message)
    : AppException(message, 409, "conflict");

public sealed class BusinessRuleException(string message)
    : AppException(message, 422, "business_rule_violation");

public sealed class ForbiddenException(string message = "Bạn không có quyền thực hiện thao tác này.")
    : AppException(message, 403, "forbidden");

public sealed class UnauthorizedException(string message = "Thông tin xác thực không hợp lệ hoặc đã hết hạn.")
    : AppException(message, 401, "unauthorized");

public sealed class AppValidationException(IDictionary<string, string[]> errors, string message = "Dữ liệu đầu vào không hợp lệ.")
    : AppException(message, 400, "validation_error", errors);
