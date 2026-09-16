using System.Linq.Expressions;
using Athkar.Shareds.Models.Base;

namespace Athkar.DataAccess.Repositories;

/// <summary>
/// Generic data access. Services depend on this — never on the DbContext.
///
/// Soft-delete lives here rather than in a global query filter: every method
/// excludes deleted rows by default, and a caller that genuinely wants them
/// (the admin console listing what an editor removed) has to say so.
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    IQueryable<T> Query(bool includeDeleted = false);

    /// <summary>Filtered query with an optional include shaper, e.g. <c>q =&gt; q.Include(x =&gt; x.Translations)</c>.</summary>
    IQueryable<T> Where(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null);

    /// <summary>First match (or null) with an optional include shaper.</summary>
    T? FirstOrDefault(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null);

    Task<T?> GetByIdAsync(int id, bool includeDeleted = false);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false);
    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false);
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false);

    void Create(T entity);
    Task AddAsync(T entity);
    Task AddRangeAsync(IEnumerable<T> entities);
    void Update(T entity);

    /// <summary>Soft-deletes the entity (sets IsDeleted + DeletionDate).</summary>
    void SoftDelete(T entity);

    /// <summary>
    /// Soft-deletes a set in one call. Used where a parent's children are
    /// replaced wholesale — a category's translations on every save, say — and
    /// looping would mean one tracked update per language.
    /// </summary>
    void SoftDeleteRange(IEnumerable<T> entities);

    /// <summary>
    /// Removes rows outright, bypassing soft delete.
    ///
    /// For rows that are not content: a dispatch that has been sent and aged
    /// out, an API log past its retention. Keeping a tombstone for those would
    /// grow a table nobody ever reads back.
    /// </summary>
    void HardDeleteRange(IEnumerable<T> entities);
}
