using BankingApp.Core.Exceptions;
using System.Net;
using System.Text.Json;

namespace BankingApp.API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    { _next = next; _logger = logger; }

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            await HandleAsync(context, ex);
        }
    }

    private static async Task HandleAsync(HttpContext context, Exception ex)
    {
        context.Response.ContentType = "application/json";
        var (status, message, errors) = ex switch
        {
            NotFoundException e => (HttpStatusCode.NotFound, e.Message, (object?)null),
            ValidationException e => (HttpStatusCode.BadRequest, e.Message, e.Errors.Any() ? (object)e.Errors : null),
            UnauthorizedException e => (HttpStatusCode.Forbidden, e.Message, (object?)null),
            InsufficientFundsException e => (HttpStatusCode.BadRequest, e.Message, (object?)null),
            _ => (HttpStatusCode.InternalServerError, "A apărut o eroare neașteptată.", (object?)null)
        };
        context.Response.StatusCode = (int)status;
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { status = (int)status, message, errors }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}
