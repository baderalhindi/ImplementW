using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PMPlatform.Domain.Common;
using PMPlatform.Domain.IdentityAccess;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>The ERD conventions (D-2, D-6, D-7, D-12) as mapping steps, so each entity configuration states them once.</summary>
internal static class EntityTypeBuilderExtensions
{
    private const int LabelLength = 200;

    /// <summary>D-6: a <see cref="BilingualLabel"/> as the pair <c>&lt;column&gt;_ar</c>, <c>&lt;column&gt;_en</c>, both present or both absent.</summary>
    public static EntityTypeBuilder<TEntity> HasBilingualLabel<TEntity>(
        this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, BilingualLabel?>> label, string column)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        ComplexPropertyBuilder<BilingualLabel> pair = builder.ComplexProperty<BilingualLabel>(PropertyName(label));
        pair.Property(l => l.Ar).HasColumnName($"{column}_ar").HasMaxLength(LabelLength);
        pair.Property(l => l.En).HasColumnName($"{column}_en").HasMaxLength(LabelLength);

        return pair.Metadata.IsNullable
            ? builder.HasCheck(column, $"(\"{column}_ar\" IS NULL) = (\"{column}_en\" IS NULL)")
            : builder;
    }

    /// <summary>D-7: a <see cref="NarrativeText"/> as <c>&lt;column&gt; text</c> with its entry language in <c>&lt;column&gt;_lang</c>, both present or both absent.</summary>
    public static EntityTypeBuilder<TEntity> HasNarrative<TEntity>(
        this EntityTypeBuilder<TEntity> builder, Expression<Func<TEntity, NarrativeText?>> narrative, string column)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        ComplexPropertyBuilder<NarrativeText> pair = builder.ComplexProperty<NarrativeText>(PropertyName(narrative));
        pair.Property(n => n.Text).HasColumnName(column);
        pair.Property(n => n.Language).HasColumnName($"{column}_lang");

        return pair.Metadata.IsNullable
            ? builder.HasCheck($"{column}_pair", $"(\"{column}\" IS NULL) = (\"{column}_lang\" IS NULL)")
            : builder;
    }

    /// <summary>D-12: the reviewer and publisher of a governed row are users.</summary>
    public static EntityTypeBuilder<TEntity> HasGovernedLifecycle<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : GovernedEntity
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ValidatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);
        return builder;
    }

    /// <summary>D-2: an APPEND_ONLY row is never updated, so its update columns equal its create columns.</summary>
    public static EntityTypeBuilder<TEntity> IsAppendOnly<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : AuditedEntity =>
        builder.HasCheck("append_only", "updated_at = created_at AND updated_by = created_by");

    /// <summary>A CHECK constraint named <c>ck_&lt;table&gt;_&lt;name&gt;</c>. Call after <c>ToTable</c>.</summary>
    public static EntityTypeBuilder<TEntity> HasCheck<TEntity>(this EntityTypeBuilder<TEntity> builder, string name, string sql)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.ToTable(table => table.HasCheckConstraint($"ck_{builder.Metadata.GetTableName()}_{name}", sql));
    }

    /// <summary>The expression is typed nullable so optional and required pairs share one helper; EF Core reads optionality from the property.</summary>
    private static string PropertyName<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> property) =>
        ((MemberExpression)property.Body).Member.Name;

    /// <summary>
    /// A database default the application can still override with any value. EF Core omits a property from an
    /// INSERT when it holds its sentinel, so the sentinel must be the default itself: with the CLR default as
    /// sentinel, explicitly setting <c>false</c> on a column that defaults to <c>true</c> would store <c>true</c>.
    /// </summary>
    public static PropertyBuilder<TProperty> HasDatabaseDefault<TProperty>(this PropertyBuilder<TProperty> property, TProperty value)
    {
        ArgumentNullException.ThrowIfNull(property);

        return property.HasDefaultValue(value).HasSentinel(value);
    }
}
