using System.Linq.Expressions;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>Predicates written one per clause, combined into one that EF Core can translate.</summary>
internal static class PredicateComposition
{
    /// <summary>The OR of <paramref name="predicates"/> over one parameter; false when there are none.</summary>
    public static Expression<Func<T, bool>> AnyOf<T>(IEnumerable<Expression<Func<T, bool>>> predicates)
    {
        ParameterExpression row = Expression.Parameter(typeof(T), "r");
        Expression body = predicates
            .Select(p => new ParameterReplacer(p.Parameters[0], row).Visit(p.Body))
            .Aggregate((Expression)Expression.Constant(false), Expression.OrElse);
        return Expression.Lambda<Func<T, bool>>(body, row);
    }

    /// <summary>The AND of two predicates over one parameter.</summary>
    public static Expression<Func<T, bool>> Both<T>(Expression<Func<T, bool>> first, Expression<Func<T, bool>> second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ParameterExpression row = Expression.Parameter(typeof(T), "r");
        return Expression.Lambda<Func<T, bool>>(
            Expression.AndAlso(new ParameterReplacer(first.Parameters[0], row).Visit(first.Body), new ParameterReplacer(second.Parameters[0], row).Visit(second.Body)),
            row);
    }

    /// <summary><paramref name="predicate"/> applied to the part of a row <paramref name="member"/> selects.</summary>
    public static Expression<Func<TRow, bool>> Over<TRow, TPart>(Expression<Func<TPart, bool>> predicate, Expression<Func<TRow, TPart>> member)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(member);
        return Expression.Lambda<Func<TRow, bool>>(new ParameterReplacer(predicate.Parameters[0], member.Body).Visit(predicate.Body), member.Parameters[0]);
    }

    private sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? replacement : base.VisitParameter(node);
    }
}
