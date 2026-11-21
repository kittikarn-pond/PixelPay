namespace PixelPay.Application.Common;

public class ApiException(int status, string? message = null)
    : Exception(message ?? $"HTTP {status}")
{
    public int Status { get; } = status;

    public string? ResponseMessage { get; } = message;
}

public class BadRequestException(string? message = null) : ApiException(400, message);

public class UnauthorizedException(string? message = null) : ApiException(401, message);

public class ForbiddenException(string? message = null) : ApiException(403, message);

public class NotFoundException(string? message = null) : ApiException(404, message);

public class ConflictException(string? message = null) : ApiException(409, message);
