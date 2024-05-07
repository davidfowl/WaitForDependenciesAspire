using Microsoft.Extensions.Diagnostics.HealthChecks;

// An annotation that associates a health check factory with a resource
public class HealthCheckAnnotation(Func<string, IHealthCheck> healthCheckFactory) : IResourceAnnotation
{
    public Func<string, IHealthCheck> HealthCheckFactory { get; } = healthCheckFactory;
}
