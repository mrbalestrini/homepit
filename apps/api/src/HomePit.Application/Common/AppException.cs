namespace HomePit.Application.Common;

public abstract class AppException(string message) : Exception(message);

public sealed class UnauthorizedException(string message) : AppException(message);

public sealed class NotFoundException(string message) : AppException(message);

public sealed class ForbiddenException(string message) : AppException(message);

public sealed class ConflictException(string message) : AppException(message);

public sealed class PreconditionRequiredException(string message) : AppException(message);

public sealed class PreconditionFailedException(string message) : AppException(message);

public sealed class LockedException(string message) : AppException(message);

public sealed class ValidationException(string message) : AppException(message);
