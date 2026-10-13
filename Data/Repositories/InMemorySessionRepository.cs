using System.Collections.Concurrent;
using Banking.Domain.Models;
using Banking.Domain.Repositories;

namespace Banking.Data.Repositories;

public sealed class InMemorySessionRepository : ISessionRepository
{
    private readonly ConcurrentDictionary<string, Session> sessions = new(StringComparer.Ordinal);

    public bool TryAdd(string token, Session session) => sessions.TryAdd(token, session);
    public Session? Get(string token) => sessions.GetValueOrDefault(token);
    public void Remove(string token) => sessions.TryRemove(token, out _);

    public void RemoveExpired(DateTimeOffset now)
    {
        foreach (var entry in sessions)
        {
            if (entry.Value.ExpiresAt <= now)
                sessions.TryRemove(entry.Key, out _);
        }
    }
}
