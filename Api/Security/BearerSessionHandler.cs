using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Banking.Api.Errors;
using Banking.Domain.Exceptions;
using Banking.Domain.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Banking.Api.Security;

public sealed class BearerSessionHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IAccessService accessService) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "BankingSession";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headers))
            return Task.FromResult(AuthenticateResult.NoResult());

        if (headers.Count != 1
            || !AuthenticationHeaderValue.TryParse(headers[0], out var header)
            || !header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(header.Parameter)
            || header.Parameter.Any(char.IsWhiteSpace))
        {
            return Task.FromResult(AuthenticateResult.Fail("Log in to continue."));
        }

        try
        {
            var session = accessService.ResolveSession(header.Parameter);
            Context.Features.Set(session);
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, session.CustomerNumber) }, SchemeName);
            return Task.FromResult(AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
        }
        catch (AuthenticationException exception)
        {
            return Task.FromResult(AuthenticateResult.Fail(exception.Message));
        }
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        var result = await HandleAuthenticateOnceAsync();
        await ApiProblem.WriteAsync(Context, StatusCodes.Status401Unauthorized,
            result.Failure?.Message ?? "Log in to continue.");
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) =>
        ApiProblem.WriteAsync(Context, StatusCodes.Status403Forbidden, "Access denied.");
}
