using Aspire.Hosting.ApplicationModel;
using HealthChecks.SqlServer;

namespace Aspire.Hosting;

public static class SqlResourceHealthCheckExtensions
{
    /// <summary>
    /// Adds a health check to the SQL Server server resource.
    /// </summary>
    public static IResourceBuilder<SqlServerServerResource> WithHealthCheck(this IResourceBuilder<SqlServerServerResource> builder)
    {
        return builder.WithAnnotation(HealthCheckAnnotation.Create(cs => new SqlServerHealthCheck(new SqlServerHealthCheckOptions { ConnectionString = cs })));
    }

    /// <summary>
    /// Adds a health check to the SQL Server database resource.
    /// </summary>
    public static IResourceBuilder<SqlServerDatabaseResource> WithHealthCheck(this IResourceBuilder<SqlServerDatabaseResource> builder)
    {
        return builder.WithAnnotation(HealthCheckAnnotation.Create(cs => new SqlServerHealthCheck(new SqlServerHealthCheckOptions { ConnectionString = cs })));
    }
}
