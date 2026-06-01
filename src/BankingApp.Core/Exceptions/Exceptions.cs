namespace BankingApp.Core.Exceptions;

public class NotFoundException : Exception
{
    public NotFoundException(string name, object key) : base($"'{name}' with key '{key}' was not found.") { }
}

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }
    public ValidationException(string message) : base(message) { Errors = new Dictionary<string, string[]>(); }
    public ValidationException(IDictionary<string, string[]> errors) : base("Validation failed.") { Errors = errors; }
}

public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("Unauthorized.") { }
    public UnauthorizedException(string message) : base(message) { }
}

public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(decimal available, decimal required)
        : base($"Insufficient funds. Available: {available:F2}, Required: {required:F2}") { }
}
