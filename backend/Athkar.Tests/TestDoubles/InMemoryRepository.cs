using System.Linq.Expressions;
using Athkar.DataAccess.Repositories;
using Athkar.Shareds.Models.Base;

namespace Athkar.Tests.TestDoubles;

/// <summary>
/// <see cref="IRepository{T}"/> over a list.
///
/// Hand-rolled rather than a mocking framework, and deliberately so: a mock
/// that returns whatever the test told it to proves the service called it, not
/// that the service is right. This behaves like the real repository in the one
/// way that matters — <b>soft-deleted rows are invisible unless asked for</b> —
/// so a test can assert on what is left rather than on which methods ran.
///
/// It does not pretend to be a database. `IQueryable` here is LINQ-to-objects,
/// so anything that only works in SQL will pass here and fail in production;
/// those paths belong in a test against a real database, not in this file.
/// </summary>
public class InMemoryRepository<T> : IRepository<T> where T : BaseEntity
{
    private readonly List<T> rows = [];
    private int nextId = 1;

    /// <summary>Everything, deleted rows included, for a test to assert on.</summary>
    public IReadOnlyList<T> All => rows;

    /// <summary>Seeds without going through Create, so ids stay predictable.</summary>
    public InMemoryRepository<T> Seed(params T[] entities)
    {
        foreach (var entity in entities)
        {
            if (entity.Id == 0) entity.Id = nextId++;
            else nextId = Math.Max(nextId, entity.Id + 1);

            rows.Add(entity);
        }

        return this;
    }

    /// <summary>
    /// Wrapped in <see cref="AsyncQueryable{T}"/> rather than returned as a
    /// plain LINQ queryable: the services call EF's async operators, and those
    /// refuse a provider that is not EF's own. See that file.
    /// </summary>
    public IQueryable<T> Query(bool includeDeleted = false) =>
        new AsyncQueryable<T>(includeDeleted ? rows : rows.Where(r => !r.IsDeleted));

    public IQueryable<T> Where(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null)
    {
        var query = Query();
        if (include is not null) query = include(query);
        return query.Where(predicate);
    }

    public T? FirstOrDefault(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null) =>
        Where(predicate, include).FirstOrDefault();

    public Task<T?> GetByIdAsync(int id, bool includeDeleted = false) =>
        Task.FromResult(Query(includeDeleted).FirstOrDefault(r => r.Id == id));

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false) =>
        Task.FromResult(Query(includeDeleted).FirstOrDefault(predicate));

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false) =>
        Task.FromResult(Query(includeDeleted).Any(predicate));

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false) =>
        Task.FromResult(predicate is null
            ? Query(includeDeleted).Count()
            : Query(includeDeleted).Count(predicate));

    public void Create(T entity)
    {
        if (entity.Id == 0) entity.Id = nextId++;
        rows.Add(entity);
    }

    public Task AddAsync(T entity)
    {
        Create(entity);
        return Task.CompletedTask;
    }

    public Task AddRangeAsync(IEnumerable<T> entities)
    {
        foreach (var entity in entities) Create(entity);
        return Task.CompletedTask;
    }

    public void Update(T entity) => entity.ModificationDate = DateTime.UtcNow;

    public void SoftDelete(T entity)
    {
        entity.IsDeleted = true;
        entity.DeletionDate = DateTime.UtcNow;
    }

    public void SoftDeleteRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities.ToList()) SoftDelete(entity);
    }

    public void HardDeleteRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities.ToList()) rows.Remove(entity);
    }
}
