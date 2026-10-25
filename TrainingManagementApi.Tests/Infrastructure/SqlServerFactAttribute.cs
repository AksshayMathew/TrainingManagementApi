namespace TrainingManagementApi.Tests.Infrastructure;

/// <summary>
/// A test that only makes sense against a real SQL Server (for example concurrency, which a single shared
/// SQLite connection cannot model). It is skipped unless <c>TMS_TEST_SQLSERVER</c> is set.
/// </summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (!ApiFactory.UsingSqlServer)
        {
            Skip = $"Set {ApiFactory.SqlServerEnvironmentVariable} to run this test against a real SQL Server.";
        }
    }
}
