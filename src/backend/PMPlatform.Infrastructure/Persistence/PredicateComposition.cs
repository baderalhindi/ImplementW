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

    private sealed class ParameterReplacer(ParameterExpression parameter, Expression replacement) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == parameter ? replacement : base.VisitParameter(node);
    }
}
