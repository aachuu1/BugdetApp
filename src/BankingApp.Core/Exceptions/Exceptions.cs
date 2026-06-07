namespace BankingApp.Core.Exceptions;

// thrown when an entity is not found in the database
// exceptionmiddleware catches this and returns http 404
public class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"'{name}' with key '{key}' was not found.") { }
}

// thrown when input data fails validation
// exceptionmiddleware catches this and returns http 400
// supports two constructors: one for a simple message, one for a dictionary of field errors
public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    // used for simple validation errors e.g. "passwords do not match"
    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    // used when identity returns multiple field-level errors
    public ValidationException(IDictionary<string, string[]> errors) : base("Validation failed.")
    {
        Errors = errors;
    }
}

// thrown when a user tries to access or modify something they don't own
// exceptionmiddleware catches this and returns http 403
public class UnauthorizedException : Exception
{
    public UnauthorizedException() : base("Unauthorized.") { }
    public UnauthorizedException(string message) : base(message) { }
}

// thrown in transactionservice and billservice when the account balance is too low
// specific to banking — gives a clear error message with exact amounts
// exceptionmiddleware catches this and returns http 400
public class InsufficientFundsException : Exception
{
    public InsufficientFundsException(decimal available, decimal required)
        : base($"Insufficient funds. Available: {available:F2}, Required: {required:F2}") { }
}