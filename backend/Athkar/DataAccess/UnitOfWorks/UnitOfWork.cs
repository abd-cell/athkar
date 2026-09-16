using Microsoft.EntityFrameworkCore.Storage;
using Athkar.DataAccess.Repositories;
using Athkar.Shareds.Attributes;
using Athkar.Shareds.Models.Base;

namespace Athkar.DataAccess.UnitOfWorks;

[ScopedInjectable]
public class UnitOfWork : IUnitOfWork
{
    private readonly DatabaseService db;
    private readonly Dictionary<Type, object> repositories = [];
    private IDbContextTransaction? transaction;

    public UnitOfWork(DatabaseService db) => this.db = db;

    public IRepository<T> Repository<T>() where T : BaseEntity
    {
        if (repositories.TryGetValue(typeof(T), out var existing))
            return (IRepository<T>)existing;

        var repository = new Repository<T>(db);
        repositories[typeof(T)] = repository;
        return repository;
    }

    public Task<int> SaveAsync() => db.SaveChangesAsync();

    public async Task BeginTransactionAsync() =>
        transaction = await db.Database.BeginTransactionAsync();

    public async Task CommitAsync()
    {
        try
        {
            await db.SaveChangesAsync();
            if (transaction is not null) await transaction.CommitAsync();
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollBackAsync()
    {
        if (transaction is not null) await transaction.RollbackAsync();
        await DisposeTransactionAsync();
    }

    public void Detach() => db.ChangeTracker.Clear();

    private async Task DisposeTransactionAsync()
    {
        if (transaction is not null)
        {
            await transaction.DisposeAsync();
            transaction = null;
        }
    }
}
