using System.ComponentModel.DataAnnotations;
using Banking.Api.Contracts;
using Banking.Api.Security;
using Banking.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/customer")]
public sealed class CustomerController(
    ICustomerService customerService,
    IAccessService accessService,
    ICurrentSession currentSession) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    public ActionResult<CustomerRegisterResponse> Register(CustomerRegisterRequest request)
    {
        var registration = customerService.Register(request.Customer.ToDomain());
        var token = accessService.StartSession(registration.Customer.CustomerNumber);
        return StatusCode(StatusCodes.Status201Created,
            new CustomerRegisterResponse(CustomerDto.FromDomain(registration.Customer), registration.Pin, token));
    }

    [HttpGet("get")]
    public ActionResult<CustomerResponse> Get([FromQuery, Required] string customerNumber)
    {
        var customer = customerService.GetCustomer(currentSession.GetRequired(), customerNumber);
        return Ok(new CustomerResponse(CustomerDto.FromDomain(customer)));
    }

    [HttpGet("accounts")]
    public ActionResult<CustomerAccountResponse> Accounts([FromQuery, Required] string customerNumber)
    {
        var accounts = customerService.GetCustomerAccounts(currentSession.GetRequired(), customerNumber);
        return Ok(new CustomerAccountResponse(customerNumber, accounts.Select(AccountDto.FromDomain).ToArray()));
    }
}
