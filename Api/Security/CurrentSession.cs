using Banking.Domain.Exceptions;
using Banking.Domain.Models;

namespace Banking.Api.Security;

public sealed class CurrentSession(IHttpContextAccessor contextAccessor) : ICurrentSession
{
    public UserSession GetRequired() =>
        contextAccessor.HttpContext?.Features.Get<UserSession>()
        ?? throw new AuthenticationException("Log in to continue.");
}
