using Banking.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace Banking.Api.Errors;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception,
        CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            AuthenticationException => StatusCodes.Status401Unauthorized,
            AccessDeniedException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            ConflictException => StatusCodes.Status409Conflict,
            ArgumentException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        if (status == StatusCodes.Status401Unauthorized)
            context.Response.Headers.WWWAuthenticate = "Bearer";

        var detail = status == StatusCodes.Status500InternalServerError
            ? "An unexpected error occurred."
            : exception.Message;
        await ApiProblem.WriteAsync(context, status, detail);
        return true;
    }
}
