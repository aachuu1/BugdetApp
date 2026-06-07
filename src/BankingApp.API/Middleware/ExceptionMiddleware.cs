using BankingApp.Core.Exceptions;
using System.Net;
using System.Text.Json;

namespace BankingApp.API.Middleware;

// catches all unhandled exceptions across the app and returns a consistent json error response
// registered in program.cs before all other middleware so nothing slips through
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            // pass the request to the next middleware in the pipeline
            await _next(context);
        }
        catch (Exception ex)
        {
            // log every unhandled exception with its full details
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleAsync(context, ex);
        }
    }

    private static async Task HandleAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";

        // map each custom exception type to the correct http status code
        var (status, message, errors) = ex switch
        {
            NotFoundException e => (HttpStatusCode.NotFound, e.Message, (object?)null),                                    // 404
            ValidationException e => (HttpStatusCode.BadRequest, e.Message, e.Errors.Any() ? (object)e.Errors : null),    // 400
            UnauthorizedException e => (HttpStatusCode.Forbidden, e.Message, (object?)null),                              // 403
            InsufficientFundsException e => (HttpStatusCode.BadRequest, e.Message, (object?)null),                        // 400 - specific to banking
            _ => (HttpStatusCode.InternalServerError, "A apărut o eroare neașteptată.", (object?)null)                    // 500 for anything else
        };

        context.Response.StatusCode = (int)status;

        // serialize the response with camelCase property names to match the frontend expectations
        await context.Response.WriteAsync(JsonSerializer.Serialize(
            new { status = (int)status, message, errors },
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
        ));
    }
}