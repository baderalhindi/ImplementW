using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PMPlatform.Domain.Common;

namespace PMPlatform.Infrastructure.Persistence;

/// <summary>Stores a <see cref="Language"/> as its ISO 639-1 code in a <c>char(2)</c> column (ERD D-7).</summary>
internal sealed class LanguageCodeConverter() : ValueConverter<Language, string>(
    language => LanguageCode.Of(language),
    code => LanguageCode.Parse(code));
