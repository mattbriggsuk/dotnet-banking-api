using Microsoft.AspNetCore.WebUtilities;

namespace Banking.Api.Errors;

internal static class ApiProblem
{
    public static Task WriteAsync(HttpContext context, int status, string detail) =>
        Results.Problem(statusCode: status, title: ReasonPhrases.GetReasonPhrase(status),
            detail: detail, instance: context.Request.Path).ExecuteAsync(context);
}
