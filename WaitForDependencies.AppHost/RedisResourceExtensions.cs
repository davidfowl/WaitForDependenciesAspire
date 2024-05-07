using HealthChecks.Redis;

public static class RedisResourceExtensions
{
    /// <summary>
    /// Adds a health check to the Redis server resource.
    /// </summary>
    public static IResourceBuilder<RedisResource> WithHealthCheck(this IResourceBuilder<RedisResource> builder)
    {
        return builder.WithAnnotation(new HealthCheckAnnotation(cs => new RedisHealthCheck(cs)));
    }
}
