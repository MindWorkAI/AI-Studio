using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AIStudio.Tools.Databases.IndexStore;

internal sealed class IndexStoreDateTimeOffsetConverter() : ValueConverter<DateTimeOffset, string>(
    value => IndexStoreDateTimeOffset.ToUtcText(value),
    value => IndexStoreDateTimeOffset.ParseUtc(value));
