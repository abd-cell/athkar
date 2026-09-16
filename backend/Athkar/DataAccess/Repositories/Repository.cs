using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Athkar.Shareds.Models.Base;

namespace Athkar.DataAccess.Repositories;

public class Repository<T> : IRepository<T> where T : BaseEntity
{
    private readonly DbSet<T> set;

    public Repository(DatabaseService db) => set = db.Set<T>();

    public IQueryable<T> Query(bool includeDeleted = false)
    {
        IQueryable<T> query = set.AsQueryable();
        return includeDeleted ? query : query.Where(x => !x.IsDeleted);
    }

    public IQueryable<T> Where(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null)
    {
        var query = Query();
        if (include != null) query = include(query);
        return query.Where(predicate);
    }

    public T? FirstOrDefault(Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>>? include = null)
    {
        var query = Query();
        if (include != null) query = include(query);
        return query.FirstOrDefault(predicate);
    }

    public void Create(T entity) => set.Add(entity);

    public Task<T?> GetByIdAsync(int id, bool includeDeleted = false) =>
        Query(includeDeleted).FirstOrDefaultAsync(x => x.Id == id);

    public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false) =>
        Query(includeDeleted).FirstOrDefaultAsync(predicate);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, bool includeDeleted = false) =>
        Query(includeDeleted).AnyAsync(predicate);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, bool includeDeleted = false) =>
        predicate is null ? Query(includeDeleted).CountAsync() : Query(includeDeleted).CountAsync(predicate);

    public async Task AddAsync(T entity) => await set.AddAsync(entity);

    public async Task AddRangeAsync(IEnumerable<T> entities) => await set.AddRangeAsync(entities);

    public void Update(T entity)
    {
        entity.ModificationDate = DateTime.UtcNow;
        set.Update(entity);
    }

    public void SoftDelete(T entity)
    {
        entity.IsDeleted = true;
        entity.DeletionDate = DateTime.UtcNow;
        set.Update(entity);
    }

    public void SoftDeleteRange(IEnumerable<T> entities)
    {
        foreach (var entity in entities) SoftDelete(entity);
    }

    public void HardDeleteRange(IEnumerable<T> entities) => set.RemoveRange(entities);
}
