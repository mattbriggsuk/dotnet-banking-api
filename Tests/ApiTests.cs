using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Banking.Domain.Models;
using Banking.Domain.Repositories;
using Banking.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Banking.Tests;

public sealed class ApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task DevelopmentHttpRequestsAreNotRedirectedWhenHttpsPortIsConfigured()
    {
        using var response = await api.SendAsync(HttpMethod.Get,
            $"/api/customer/get?customerNumber={api.Alice.Number}", token: api.Alice.Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData("/api/customer/get?customerNumber=1234567890123452", "GET")]
    [InlineData("/api/customer/accounts?customerNumber=1234567890123452", "GET")]
    [InlineData("/api/access/logout", "POST")]
    [InlineData("/api/account/create", "POST")]
    [InlineData("/api/account/close", "POST")]
    [InlineData("/api/account/deposit", "POST")]
    [InlineData("/api/account/withdraw", "POST")]
    [InlineData("/api/account/transfer", "POST")]
    public async Task EveryProtectedRouteChallengesAnonymousRequests(string path, string method)
    {
        using var response = await api.SendAsync(new HttpMethod(method), path);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Bearer", response.Headers.WwwAuthenticate.Single().Scheme);
    }

    [Theory]
    [InlineData("Basic abc")]
    [InlineData("Bearer")]
    [InlineData("Bearer unknown")]
    [InlineData("Bearer token with spaces")]
    public async Task InvalidAuthorizationHeadersReturn401(string authorization)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/customer/get?customerNumber={api.Alice.Number}");
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        using var response = await api.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task InvalidTokenOnPublicEndpointIsRejected()
    {
        using var response = await api.PostAsync("/api/access/login",
            new { identifier = api.Alice.Email, pin = api.Alice.Pin }, "invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ExpiredSessionsReturn401()
    {
        var token = Guid.NewGuid().ToString("N");
        api.Services.GetRequiredService<ISessionRepository>().TryAdd(token,
            new Session(api.Alice.Number, DateTimeOffset.UtcNow.AddMinutes(-1)));
        using var response = await api.SendAsync(HttpMethod.Get,
            $"/api/customer/get?customerNumber={api.Alice.Number}", token: token);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CustomerDetailsAndAccountsRequireOwnership()
    {
        foreach (var endpoint in new[] { "get", "accounts" })
        {
            using var own = await api.SendAsync(HttpMethod.Get,
                $"/api/customer/{endpoint}?customerNumber={api.Alice.Number}", token: api.Alice.Token);
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            using var other = await api.SendAsync(HttpMethod.Get,
                $"/api/customer/{endpoint}?customerNumber={api.Bob.Number}", token: api.Alice.Token);
            Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        }
    }

    [Theory]
    [InlineData("CURRENT")]
    [InlineData("SAVINGS")]
    [InlineData("CREDIT")]
    public async Task AccountCreationPersistsTypeAndUsesAuthenticatedCustomer(string type)
    {
        using var response = await api.PostAsync("/api/account/create",
            new { accountType = type, customerNumber = api.Bob.Number }, api.Alice.Token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var account = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("account");
        Assert.Equal(type, account.GetProperty("accountType").GetString());
        Assert.Equal(api.Alice.Number, account.GetProperty("customerNumber").GetString());
    }

    [Theory]
    [InlineData("{}", 201)]
    [InlineData("{\"accountType\":null}", 400)]
    [InlineData("{\"accountType\":\"INVALID\"}", 400)]
    [InlineData("{\"accountType\":99}", 400)]
    [InlineData("{\"accountType\":0}", 400)]
    [InlineData("{", 400)]
    [InlineData("null", 400)]
    public async Task AccountCreationValidatesJsonAndDefaultsToCurrent(string json, int status)
    {
        using var response = await api.SendAsync(HttpMethod.Post, "/api/account/create",
            new StringContent(json, Encoding.UTF8, "application/json"), api.Alice.Token);
        Assert.Equal(status, (int)response.StatusCode);
        if (status == 201)
        {
            var account = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("account");
            Assert.Equal("CURRENT", account.GetProperty("accountType").GetString());
        }
    }

    [Fact]
    public async Task DuplicateRegistrationReturns409AndLoginAcceptsEmailOrNumber()
    {
        using var duplicate = await api.PostAsync("/api/customer/register",
            new { customer = new { firstName = "Other", lastName = "User", email = api.Alice.Email.ToUpperInvariant() } });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        foreach (var identifier in new[] { api.Alice.Number, $" {api.Alice.Email.ToUpperInvariant()} " })
        {
            using var login = await api.PostAsync("/api/access/login", new { identifier, pin = api.Alice.Pin });
            Assert.Equal(HttpStatusCode.OK, login.StatusCode);
            var body = await login.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(2, body.EnumerateObject().Count());
            Assert.Equal(api.Alice.Number, body.GetProperty("customer").GetProperty("customerNumber").GetString());
        }
    }

    [Fact]
    public async Task WrongCredentialsUseGeneric401AndLogoutRevokesOnlyItsToken()
    {
        var wrongPin = api.Alice.Pin == "999999" ? "888888" : "999999";
        using var wrong = await api.PostAsync("/api/access/login", new { identifier = api.Alice.Email, pin = wrongPin });
        using var unknown = await api.PostAsync("/api/access/login", new { identifier = "unknown@example.com", pin = wrongPin });
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal((await wrong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString(),
            (await unknown.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString());
        using var login = await api.PostAsync("/api/access/login", new { identifier = api.Alice.Email, pin = api.Alice.Pin });
        var token = (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sessionToken").GetString()!;
        using var logout = await api.PostAsync("/api/access/logout", null, token);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        using var revoked = await api.SendAsync(HttpMethod.Get, $"/api/customer/get?customerNumber={api.Alice.Number}", token: token);
        Assert.Equal(HttpStatusCode.Unauthorized, revoked.StatusCode);
        using var original = await api.SendAsync(HttpMethod.Get, $"/api/customer/get?customerNumber={api.Alice.Number}", token: api.Alice.Token);
        Assert.Equal(HttpStatusCode.OK, original.StatusCode);
    }

    [Fact]
    public async Task AccountOperationsEnforceOwnershipAndTransferAcrossCustomers()
    {
        var source = await api.CreateAccountAsync(api.Alice.Token);
        var target = await api.CreateAccountAsync(api.Bob.Token);
        var deposit = new { transaction = new { transactionType = "DEPOSIT", amount = 10.25m, targetAccountNumber = source } };
        using var funded = await api.PostAsync("/api/account/deposit", deposit, api.Alice.Token);
        Assert.Equal(HttpStatusCode.OK, funded.StatusCode);
        using var forbiddenDeposit = await api.PostAsync("/api/account/deposit", deposit, api.Bob.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDeposit.StatusCode);
        using var forbiddenWithdrawal = await api.PostAsync("/api/account/withdraw",
            new { transaction = new { transactionType = "WITHDRAWAL", amount = 1m, targetAccountNumber = source } }, api.Bob.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWithdrawal.StatusCode);
        using var forbiddenClose = await api.PostAsync("/api/account/close", new { accountNumber = source }, api.Bob.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenClose.StatusCode);
        var transfer = new { transaction = new { transactionType = "TRANSFER", amount = 2.25m, sourceAccountNumber = source, targetAccountNumber = target } };
        using var forbiddenTransfer = await api.PostAsync("/api/account/transfer", transfer, api.Bob.Token);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenTransfer.StatusCode);
        using var transferred = await api.PostAsync("/api/account/transfer", transfer, api.Alice.Token);
        Assert.Equal(HttpStatusCode.OK, transferred.StatusCode);
        var body = await transferred.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(8m, body.GetProperty("fromAccount").GetProperty("balance").GetDecimal());
        Assert.Equal(2.25m, body.GetProperty("toAccount").GetProperty("balance").GetDecimal());
    }

    [Fact]
    public async Task AccountLifecycleReturnsExpectedConflictAndAcceptedStatuses()
    {
        var number = await api.CreateAccountAsync(api.Alice.Token);
        using var deposit = await api.PostAsync("/api/account/deposit", Movement("DEPOSIT", number, 5m), api.Alice.Token);
        Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
        using var nonzero = await api.PostAsync("/api/account/close", new { accountNumber = number }, api.Alice.Token);
        Assert.Equal(HttpStatusCode.Conflict, nonzero.StatusCode);
        using var insufficient = await api.PostAsync("/api/account/withdraw", Movement("WITHDRAWAL", number, 6m), api.Alice.Token);
        Assert.Equal(HttpStatusCode.Conflict, insufficient.StatusCode);
        using var withdrawn = await api.PostAsync("/api/account/withdraw", Movement("WITHDRAWAL", number, 5m), api.Alice.Token);
        Assert.Equal(HttpStatusCode.OK, withdrawn.StatusCode);
        using var closed = await api.PostAsync("/api/account/close", new { accountNumber = number }, api.Alice.Token);
        Assert.Equal(HttpStatusCode.Accepted, closed.StatusCode);
        Assert.True((await closed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("account").GetProperty("closed").GetBoolean());
        using var afterClose = await api.PostAsync("/api/account/deposit", Movement("DEPOSIT", number, 1m), api.Alice.Token);
        Assert.Equal(HttpStatusCode.Conflict, afterClose.StatusCode);
        using var missing = await api.PostAsync("/api/account/close", new { accountNumber = AccountUtils.GenerateAccountNumber() }, api.Alice.Token);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Theory]
    [InlineData("/api/customer/register", "{}")]
    [InlineData("/api/customer/register", "{\"customer\":{\"firstName\":\" \",\"lastName\":\"Smith\",\"email\":\"alice@example.com\"}}")]
    [InlineData("/api/customer/register", "{\"customer\":{\"firstName\":\"Alice\",\"lastName\":\"Smith\",\"email\":\"invalid\"}}")]
    [InlineData("/api/access/login", "{\"identifier\":\"alice@example.com\"}")]
    [InlineData("/api/access/login", "{\"identifier\":\"alice@example.com\",\"pin\":\"123\"}")]
    [InlineData("/api/account/deposit", "{}")]
    [InlineData("/api/account/deposit", "{\"transaction\":{}}")]
    [InlineData("/api/account/deposit", "{\"transaction\":{\"transactionType\":\"DEPOSIT\",\"amount\":0,\"targetAccountNumber\":\"1234567890123452\"}}")]
    [InlineData("/api/account/deposit", "{\"transaction\":{\"transactionType\":\"DEPOSIT\",\"amount\":0.001,\"targetAccountNumber\":\"1234567890123452\"}}")]
    [InlineData("/api/account/deposit", "{\"transaction\":{\"transactionType\":\"WITHDRAWAL\",\"amount\":1,\"targetAccountNumber\":\"1234567890123452\"}}")]
    [InlineData("/api/account/transfer", "{\"transaction\":{\"transactionType\":\"TRANSFER\",\"amount\":1,\"targetAccountNumber\":\"1234567890123452\"}}")]
    public async Task InvalidRequestsReturn400ProblemDetails(string path, string json)
    {
        using var response = await api.SendAsync(HttpMethod.Post, path,
            new StringContent(json, Encoding.UTF8, "application/json"), api.Alice.Token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(400, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task MissingAndInvalidQueryParametersReturn400()
    {
        foreach (var path in new[] { "/api/customer/get", "/api/customer/accounts", "/api/customer/get?customerNumber=1234567890123456" })
        {
            using var response = await api.SendAsync(HttpMethod.Get, path, token: api.Alice.Token);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    private static object Movement(string type, string target, decimal amount) =>
        new { transaction = new { transactionType = type, amount, targetAccountNumber = target } };
}
