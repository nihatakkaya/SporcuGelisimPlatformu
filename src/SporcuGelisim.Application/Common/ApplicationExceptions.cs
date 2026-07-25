namespace SporcuGelisim.Application.Common;

public abstract class ApplicationExceptionBase(string message) : Exception(message);

public sealed class NotFoundException(string message) : ApplicationExceptionBase(message);

public sealed class ForbiddenException(string message) : ApplicationExceptionBase(message);

public sealed class ConflictException(string message) : ApplicationExceptionBase(message);

public sealed class ValidationFailedException(IReadOnlyDictionary<string, string[]> errors)
    : ApplicationExceptionBase("Validation failed.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
