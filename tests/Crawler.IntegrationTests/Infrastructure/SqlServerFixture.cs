using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;

namespace Crawler.IntegrationTests.Infrastructure;

/// <summary>One SQL Server container for the whole test run; each test gets its own database.</summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    public string CreateDatabaseConnectionString() =>
        new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"crawler_test_{Guid.NewGuid():N}"
        }.ConnectionString;
}

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
