using Banking.Domain.Models;

namespace Banking.Api.Security;

public interface ICurrentSession
{
    UserSession GetRequired();
}
