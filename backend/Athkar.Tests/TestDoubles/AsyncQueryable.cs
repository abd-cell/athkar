using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;

namespace Athkar.Tests.TestDoubles;

/// <summary>
/// Lets EF Core's async operators run over a plain list.
///
/// `FirstOrDefaultAsync`, `ToListAsync` and the rest are EF extension methods
/// that demand an <see cref="IAsyncQueryProvider"/>; handed an ordinary LINQ
/// queryable they throw rather than falling back. Since the services under test
/// call those methods — correctly, they are talking to a database in
/// production — the double has to hand back a queryable that satisfies them.
///
/// Everything here executes synchronously against the list and wraps the result
/// in a completed task. It is a shim, not a database: it makes the *shape* of
/// the call work so the service's logic can be tested, and it says nothing
/// about whether a query would translate to SQL.
/// </summary>
internal sealed class AsyncQueryable<T> : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    private readonly IQueryable<T> source;

    public AsyncQueryable(IEnumerable<T> items)
    {
        source = items.AsQueryable();
        Expression = source.Expression;
        Provider = new AsyncQueryProvider(source.Provider);
    }

    public AsyncQueryable(IQueryable<T> source, Expression expression)
    {
        this.source = source;
        Expression = IncludeStripper.Strip(expression);
        Provider = new AsyncQueryProvider(source.Provider);
    }

    public Type ElementType => typeof(T);

    public Expression Expression { get; }

    public IQueryProvider Provider { get; }

    public IEnumerator<T> GetEnumerator() =>
        source.Provider.CreateQuery<T>(Expression).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public async IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        foreach (var item in source.Provider.CreateQuery<T>(Expression))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return item;
        }

        await Task.CompletedTask;
    }
}

/// <summary>
/// Wraps the plain LINQ provider so every query derived from an
/// <see cref="AsyncQueryable{T}"/> stays async-capable — including the ones the
/// operators build, which is where a naive shim comes apart.
/// </summary>
internal sealed class AsyncQueryProvider : IAsyncQueryProvider
{
    private readonly IQueryProvider inner;

    public AsyncQueryProvider(IQueryProvider inner) => this.inner = inner;

    public IQueryable CreateQuery(Expression expression) =>
        (IQueryable)Activator.CreateInstance(
            typeof(AsyncQueryable<>).MakeGenericType(ElementTypeOf(expression)),
            inner.CreateQuery(expression),
            expression)!;

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
        new AsyncQueryable<TElement>(inner.CreateQuery<TElement>(expression), expression);

    public object? Execute(Expression expression) => inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);

    /// <summary>
    /// EF asks for a `Task&lt;TResult&gt;`. The work itself is synchronous here, so
    /// the answer is computed and wrapped — through reflection, because the
    /// task's type argument is only known at run time.
    /// </summary>
    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        var resultType = typeof(TResult).IsGenericType
            ? typeof(TResult).GetGenericArguments()[0]
            : typeof(TResult);

        var result = typeof(IQueryProvider)
            .GetMethods()
            .First(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
            .MakeGenericMethod(resultType)
            .Invoke(inner, [expression]);

        return (TResult)typeof(Task)
            .GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(resultType)
            .Invoke(null, [result])!;
    }

    private static Type ElementTypeOf(Expression expression) =>
        expression.Type.IsGenericType
            ? expression.Type.GetGenericArguments()[0]
            : expression.Type;
}

/// <summary>
/// Removes <c>Include</c> / <c>ThenInclude</c> from a query before a plain LINQ
/// provider is asked to run it.
///
/// They have no LINQ-to-objects equivalent, so leaving them in makes any query
/// that eager-loads throw — which would put the dispatcher, the one service
/// whose behaviour most needs testing, out of reach of these doubles
/// altogether. Dropping them is sound because the thing they do is populate
/// navigation properties, and an in-memory test populates those by simply
/// assigning them: the object graph the service sees is the same one EF would
/// have materialised.
///
/// It does mean these tests cannot catch a *missing* Include. That is the same
/// blind spot <see cref="FakeUnitOfWork"/> has for a missing save, and it is
/// recorded here for the same reason.
/// </summary>
internal sealed class IncludeStripper : ExpressionVisitor
{
    private static readonly IncludeStripper Instance = new();

    public static Expression Strip(Expression expression) => Instance.Visit(expression);

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.DeclaringType == typeof(EntityFrameworkQueryableExtensions) &&
            node.Method.Name is nameof(EntityFrameworkQueryableExtensions.Include)
                or nameof(EntityFrameworkQueryableExtensions.ThenInclude))
        {
            // The source is the first argument; everything else is the path
            // being included, which has nowhere to go here.
            return Visit(node.Arguments[0]);
        }

        return base.VisitMethodCall(node);
    }
}
