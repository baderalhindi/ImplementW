using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>
/// Gives every column that stores an enum or a <see cref="Language"/> a CHECK constraint listing the values it may
/// hold, so a state, type or language column cannot hold a value the domain does not define (ERD §6, D-7). It runs
/// when the model is finalised, after the snake_case naming convention, so the constraint names the real column.
/// </summary>
internal sealed class ValueSetCheckConstraintConvention : IModelFinalizingConvention
{
    private static readonly string[] LanguageCodes = ["ar", "en"];

    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (IConventionEntityType entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            string table = entityType.GetTableName()!;
            IEnumerable<IConventionProperty> properties = entityType.GetProperties()
                .Concat(entityType.GetComplexProperties().SelectMany(complex => complex.ComplexType.GetProperties()));

            foreach (IConventionProperty property in properties)
            {
                if (AllowedValues(property.ClrType) is not { } values)
                {
                    continue;
                }

                string column = property.GetColumnName();
                string list = string.Join(", ", values.Select(value => $"'{value}'"));
                entityType.Builder.HasCheckConstraint($"ck_{table}_{column}", $"\"{column}\" IN ({list})");
            }
        }
    }

    private static IReadOnlyList<string>? AllowedValues(Type clrType)
    {
        Type type = Nullable.GetUnderlyingType(clrType) ?? clrType;

        return type == typeof(Language) ? LanguageCodes
            : type.IsEnum ? (IReadOnlyList<string>)typeof(EnumText<>).MakeGenericType(type)
                .GetProperty(nameof(EnumText<>.All), BindingFlags.Public | BindingFlags.Static)!
                .GetValue(null)!
            : null;
    }
}
