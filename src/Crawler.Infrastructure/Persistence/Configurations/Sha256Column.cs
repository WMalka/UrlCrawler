namespace Crawler.Infrastructure.Persistence.Configurations;

internal static class Sha256Column
{
    public const string ColumnType = "binary(32)";

    /// <summary>
    /// Persisted computed hash used for unique indexes: nvarchar(2048) exceeds SQL Server's
    /// 1700-byte index key limit. Lookups must hash an nvarchar parameter the same way.
    /// </summary>
    public static string Of(string column) => $"CAST(HASHBYTES('SHA2_256', [{column}]) AS binary(32))";
}
