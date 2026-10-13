using Banking.Api.Contracts;
using Banking.Api.Security;
using Banking.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/access")]
public sealed class AccessController(IAccessService accessService, ICurrentSession currentSession) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public ActionResult<CustomerLoginResponse> Login(CustomerLoginRequest request)
    {
        var session = accessService.Login(request.Identifier, request.Pin);
        return Ok(new CustomerLoginResponse(CustomerDto.FromDomain(session.Customer), session.SessionToken));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        accessService.Logout(currentSession.GetRequired());
        return NoContent();
    }
}
