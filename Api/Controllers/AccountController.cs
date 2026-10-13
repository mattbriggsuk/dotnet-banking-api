using Banking.Api.Contracts;
using Banking.Api.Security;
using Banking.Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account")]
public sealed class AccountController(IAccountService accountService, ICurrentSession currentSession) : ControllerBase
{
    [HttpPost("create")]
    public ActionResult<AccountResponse> Create(AccountCreateRequest request)
    {
        var account = accountService.Create(currentSession.GetRequired(), request.AccountType!.Value);
        return StatusCode(StatusCodes.Status201Created, new AccountResponse(AccountDto.FromDomain(account)));
    }

    [HttpPost("close")]
    public ActionResult<AccountResponse> Close(AccountCloseRequest request)
    {
        var account = accountService.Close(currentSession.GetRequired(), request.AccountNumber);
        return Accepted(new AccountResponse(AccountDto.FromDomain(account)));
    }

    [HttpPost("deposit")]
    public ActionResult<AccountResponse> Deposit(AccountTransactionRequest request)
    {
        var account = accountService.Deposit(currentSession.GetRequired(), request.Transaction.ToDomain());
        return Ok(new AccountResponse(AccountDto.FromDomain(account)));
    }

    [HttpPost("withdraw")]
    public ActionResult<AccountResponse> Withdraw(AccountTransactionRequest request)
    {
        var account = accountService.Withdraw(currentSession.GetRequired(), request.Transaction.ToDomain());
        return Ok(new AccountResponse(AccountDto.FromDomain(account)));
    }

    [HttpPost("transfer")]
    public ActionResult<AccountTransferResponse> Transfer(AccountTransactionRequest request)
    {
        var result = accountService.Transfer(currentSession.GetRequired(), request.Transaction.ToDomain());
        return Ok(new AccountTransferResponse(AccountDto.FromDomain(result.FromAccount), AccountDto.FromDomain(result.ToAccount)));
    }
}
