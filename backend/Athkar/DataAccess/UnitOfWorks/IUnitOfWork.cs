using Athkar.DataAccess.Repositories;
using Athkar.Shareds.Models.Base;

namespace Athkar.DataAccess.UnitOfWorks;

/// <summary>
/// Coordinates repositories and the transaction boundary. Multi-row invariants
/// (claiming a broadcast for sending, replacing a dhikr's translations) wrap
/// Begin/Commit/RollBack.
/// </summary>
public interface IUnitOfWork
{
    IRepository<T> Repository<T>() where T : BaseEntity;

    Task<int> SaveAsync();

    Task BeginTransactionAsync();
    Task CommitAsync();
    Task RollBackAsync();

    /// <summary>
    /// Forgets every tracked entity, so the next read comes from the database
    /// rather than from the identity map. Needed to retry an operation that lost
    /// a concurrency race — the stale instance is otherwise still tracked and
    /// every retry would fail on the same stale token.
    /// </summary>
    void Detach();
}
