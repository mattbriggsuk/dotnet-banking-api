using Banking.Domain.Models;

namespace Banking.Domain.Services;

public interface IAccessService
{
    UserSession Login(string identifier, string pin);
    string StartSession(string customerNumber);
    UserSession ResolveSession(string token);
    void Logout(UserSession userSession);
}
