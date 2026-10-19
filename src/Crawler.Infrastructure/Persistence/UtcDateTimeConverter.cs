using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Crawler.Infrastructure.Persistence;

/// <summary>
/// All timestamps are stored as UTC in datetime2. SQL Server drops DateTimeKind, so this marks values read
/// back as UTC; otherwise they serialize without "Z" and clients read them as local time.
/// </summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));
