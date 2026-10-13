using System.Security.Cryptography;
using Banking.Domain.Exceptions;
using Banking.Domain.Models;
using Banking.Domain.Repositories;
using Banking.Domain.Services;
using Banking.Utils;

namespace Banking.Business.Services;

public sealed class AccessService(
    ICustomerRepository customerRepository,
    ISessionRepository sessionRepository,
    TimeProvider timeProvider) : IAccessService
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(30);
    private readonly string dummyHash = PinUtils.Hash(PinUtils.Generate());

    public UserSession Login(string identifier, string pin)
    {
        var normalized = identifier?.Trim().ToLowerInvariant() ?? string.Empty;
        var customer = normalized.Contains('@')
            ? customerRepository.GetCustomerByEmail(normalized)
            : customerRepository.GetCustomer(normalized);
        var hash = customer is null ? null : customerRepository.GetPinHash(customer.CustomerNumber);
        var matches = PinUtils.Matches(pin, hash ?? dummyHash);

        if (customer is null || hash is null || !matches)
            throw new AuthenticationException("Customer details or PIN are incorrect.");

        return new UserSession(customer, StartSession(customer.CustomerNumber));
    }

    public string StartSession(string customerNumber)
    {
        if (customerRepository.GetCustomer(customerNumber) is null)
            throw new NotFoundException("Customer not found.");

        var now = timeProvider.GetUtcNow();
        sessionRepository.RemoveExpired(now);
        string token;
        do
        {
            token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        } while (!sessionRepository.TryAdd(token, new Session(customerNumber, now + SessionLifetime)));

        return token;
    }

    public UserSession ResolveSession(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new AuthenticationException("Log in to continue.");

        var session = sessionRepository.Get(token);
        if (session is null || session.ExpiresAt <= timeProvider.GetUtcNow())
        {
            sessionRepository.Remove(token);
            throw new AuthenticationException("Your session has expired. Log in again.");
        }

        var customer = customerRepository.GetCustomer(session.CustomerNumber)
            ?? throw new AuthenticationException("Your session has expired. Log in again.");
        return new UserSession(customer, token);
    }

    public void Logout(UserSession userSession) => sessionRepository.Remove(userSession.SessionToken);
}
