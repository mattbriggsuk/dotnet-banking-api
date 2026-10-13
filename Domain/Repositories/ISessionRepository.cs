using Banking.Domain.Models;

namespace Banking.Domain.Repositories;

public interface ISessionRepository
{
    bool TryAdd(string token, Session session);
    Session? Get(string token);
    void Remove(string token);
    void RemoveExpired(DateTimeOffset now);
}
