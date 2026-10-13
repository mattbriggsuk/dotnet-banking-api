using Banking.Business.Services;
using Banking.Data.Repositories;
using Banking.Domain.Exceptions;
using Banking.Domain.Models;
using Banking.Utils;

namespace Banking.Tests;

public sealed class CustomerAndAccessTests
{
    private readonly InMemoryCustomerRepository customers = new();
    private readonly InMemoryAccountRepository accounts = new();
    private readonly InMemorySessionRepository sessions = new();
    private readonly TestClock clock = new();

    [Fact]
    public void RegistrationGeneratesCredentialsAndNormalizesDetails()
    {
        var service = new CustomerService(customers, accounts);
        var registration = service.Register(new Customer("ignored", " Alice ", " Smith ", " ALICE@EXAMPLE.COM "));
        Assert.True(ValidationUtils.IsValidCustomerNumber(registration.Customer.CustomerNumber));
        Assert.Equal("Alice", registration.Customer.FirstName);
        Assert.Equal("Smith", registration.Customer.LastName);
        Assert.Equal("alice@example.com", registration.Customer.Email);
        Assert.Matches("^[0-9]{6}$", registration.Pin);
        var hash = customers.GetPinHash(registration.Customer.CustomerNumber)!;
        Assert.NotEqual(registration.Pin, hash);
        Assert.True(PinUtils.Matches(registration.Pin, hash));
        Assert.Equal(registration.Customer, customers.GetCustomerByEmail(" ALICE@example.com "));
        Assert.DoesNotContain(registration.Pin, registration.ToString());
    }

    [Fact]
    public void DuplicateEmailCannotReplaceCustomerOrCredentials()
    {
        var service = new CustomerService(customers, accounts);
        var first = service.Register(new Customer("", "Alice", "Smith", "alice@example.com"));
        var hash = customers.GetPinHash(first.Customer.CustomerNumber);
        Assert.Throws<ConflictException>(() => service.Register(new Customer("", "Other", "Name", "ALICE@EXAMPLE.COM")));
        Assert.Equal(first.Customer, customers.GetCustomerByEmail("alice@example.com"));
        Assert.Equal(hash, customers.GetPinHash(first.Customer.CustomerNumber));
    }

    [Theory]
    [InlineData("", "Smith", "alice@example.com")]
    [InlineData("Alice", " ", "alice@example.com")]
    [InlineData("Alice", "Smith", "not-an-email")]
    public void InvalidRegistrationIsRejected(string firstName, string lastName, string email)
    {
        var service = new CustomerService(customers, accounts);
        Assert.Throws<ArgumentException>(() => service.Register(new Customer("", firstName, lastName, email)));
    }

    [Fact]
    public void CustomerLookupChecksOwnershipExistenceAndChecksum()
    {
        var customer = SeedCustomer();
        var service = new CustomerService(customers, accounts);
        var session = new UserSession(customer, "test");
        Assert.Equal(customer, service.GetCustomer(session, customer.CustomerNumber));
        Assert.Empty(service.GetCustomerAccounts(session, customer.CustomerNumber));
        Assert.Throws<ArgumentException>(() => service.GetCustomer(session, "1234567890123456"));
        Assert.Throws<AccessDeniedException>(() => service.GetCustomer(session, AccountUtils.GenerateAccountNumber()));
        var missing = customer with { CustomerNumber = AccountUtils.GenerateAccountNumber() };
        Assert.Throws<NotFoundException>(() => service.GetCustomer(new UserSession(missing, "test"), missing.CustomerNumber));
    }

    [Fact]
    public void LoginAcceptsCustomerNumberOrNormalizedEmailAndLeadingZeroPin()
    {
        var customer = SeedCustomer();
        var service = new AccessService(customers, sessions, clock);
        var byNumber = service.Login(customer.CustomerNumber, "012345");
        var byEmail = service.Login(" ALICE@EXAMPLE.COM ", "012345");
        Assert.Equal(customer, byNumber.Customer);
        Assert.Equal(customer, byEmail.Customer);
        Assert.NotEqual(byNumber.SessionToken, byEmail.SessionToken);
        Assert.Equal(byNumber, service.ResolveSession(byNumber.SessionToken));
        Assert.Matches("^[A-Za-z0-9_-]{43}$", byNumber.SessionToken);
        Assert.Equal(clock.Now.AddMinutes(30), sessions.Get(byNumber.SessionToken)!.ExpiresAt);
    }

    [Fact]
    public void IncorrectCredentialsReturnTheSameMessage()
    {
        SeedCustomer();
        var service = new AccessService(customers, sessions, clock);
        var wrong = Assert.Throws<AuthenticationException>(() => service.Login("alice@example.com", "999999"));
        var missing = Assert.Throws<AuthenticationException>(() => service.Login("missing@example.com", "012345"));
        Assert.Equal(wrong.Message, missing.Message);
        Assert.Throws<AuthenticationException>(() => service.Login("alice@example.com", null!));
    }

    [Fact]
    public void LogoutAndExactExpiryRevokeTokens()
    {
        var customer = SeedCustomer();
        var service = new AccessService(customers, sessions, clock);
        var token = service.StartSession(customer.CustomerNumber);
        var session = service.ResolveSession(token);
        service.Logout(session);
        Assert.Throws<AuthenticationException>(() => service.ResolveSession(token));
        token = service.StartSession(customer.CustomerNumber);
        clock.Now = clock.Now.AddMinutes(30).AddTicks(-1);
        Assert.Equal(customer, service.ResolveSession(token).Customer);
        clock.Now = clock.Now.AddTicks(1);
        Assert.Throws<AuthenticationException>(() => service.ResolveSession(token));
        Assert.Null(sessions.Get(token));
        Assert.Throws<AuthenticationException>(() => service.ResolveSession(""));
        Assert.Throws<AuthenticationException>(() => service.ResolveSession("unknown"));
    }

    [Fact]
    public void StartingSessionCleansExpiredEntriesAndRejectsMissingCustomer()
    {
        var customer = SeedCustomer();
        var service = new AccessService(customers, sessions, clock);
        sessions.TryAdd("expired", new Session(customer.CustomerNumber, clock.Now));
        service.StartSession(customer.CustomerNumber);
        Assert.Null(sessions.Get("expired"));
        Assert.Throws<NotFoundException>(() => service.StartSession(AccountUtils.GenerateAccountNumber()));
    }

    [Fact]
    public void PinHashesUseIndependentSaltsAndRejectMalformedHashes()
    {
        var first = PinUtils.Hash("012345");
        var second = PinUtils.Hash("012345");
        Assert.NotEqual(first, second);
        Assert.True(PinUtils.Matches("012345", first));
        Assert.False(PinUtils.Matches("123456", first));
        Assert.False(PinUtils.Matches("012345", "invalid"));
        Assert.False(PinUtils.Matches("012345", "!:!"));
        Assert.False(PinUtils.Matches("012345", ":"));
    }

    private Customer SeedCustomer() => customers.Save(
        new Customer(AccountUtils.GenerateAccountNumber(), "Alice", "Smith", "alice@example.com"), PinUtils.Hash("012345"));
}
