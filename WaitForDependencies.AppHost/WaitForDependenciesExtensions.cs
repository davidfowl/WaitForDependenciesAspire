using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using Aspire.Hosting.Lifecycle;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

public static class WaitForDependenciesExtensions
{
    /// <summary>
    /// Wait for a resource to be running before starting another resource.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="other">The resource to wait for.</param>
    public static IResourceBuilder<T> WaitOn<T>(this IResourceBuilder<T> builder, IResourceBuilder<IResource> other)
        where T : IResource
    {
        builder.ApplicationBuilder.AddWaitForDependencies();
        return builder.WithAnnotation(new WaitOnAnnotation(other.Resource));
    }

    /// <summary>
    /// Wait for a resource to run to completion before starting another resource.
    /// </summary>
    /// <typeparam name="T">The resource type.</typeparam>
    /// <param name="builder">The resource builder.</param>
    /// <param name="other">The resource to wait for.</param>
    public static IResourceBuilder<T> WaitForCompletion<T>(this IResourceBuilder<T> builder, IResourceBuilder<IResource> other)
        where T : IResource
    {
        builder.ApplicationBuilder.AddWaitForDependencies();
        return builder.WithAnnotation(new WaitOnAnnotation(other.Resource) { WaitUntilCompleted = true });
    }

    /// <summary>
    /// Adds a lifecycle hook that waits for all dependencies to be "running" before starting resources. If that resource
    /// has a health check, it will be executed before the resource is considered "running".
    /// </summary>
    /// <param name="builder">The <see cref="IDistributedApplicationBuilder"/>.</param>
    private static IDistributedApplicationBuilder AddWaitForDependencies(this IDistributedApplicationBuilder builder)
    {
        builder.Services.TryAddLifecycleHook<WaitForDependenciesRunningHook>();
        return builder;
    }

    private class WaitOnAnnotation(IResource resource) : IResourceAnnotation
    {
        public IResource Resource { get; } = resource;

        public string[]? States { get; set; }

        public bool WaitUntilCompleted { get; set; }
    }

    private class WaitForDependenciesRunningHook(DistributedApplicationExecutionContext executionContext,
        ResourceNotificationService resourceNotificationService) :
        IDistributedApplicationLifecycleHook,
        IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        public Task BeforeStartAsync(DistributedApplicationModel appModel, CancellationToken cancellationToken = default)
        {
            // We don't need to execute any of this logic in publish mode
            if (executionContext.IsPublishMode)
            {
                return Task.CompletedTask;
            }

            // The global list of resources being waited on
            var waitingResources = new ConcurrentDictionary<IResource, ConcurrentDictionary<WaitOnAnnotation, TaskCompletionSource>>();

            // For each resource, add an environment callback that waits for dependencies to be running
            foreach (var r in appModel.Resources)
            {
                var resourcesToWaitOn = r.Annotations.OfType<WaitOnAnnotation>().ToLookup(a => a.Resource);

                if (resourcesToWaitOn.Count == 0)
                {
                    continue;
                }

                // Abuse the environment callback to wait for dependencies to be running

                r.Annotations.Add(new EnvironmentCallbackAnnotation(async context =>
                {
                    var dependencies = new List<Task>();

                    // Find connection strings and endpoint references and get the resource they point to
                    foreach (var g in resourcesToWaitOn)
                    {
                        var resource = g.Key;

                        // REVIEW: This logic does not handle cycles in the dependency graph (that would result in a deadlock)

                        // Don't wait for yourself
                        if (resource != r && resource is not null)
                        {
                            var pendingAnnotations = waitingResources.GetOrAdd(resource, _ => new());

                            foreach (var a in g)
                            {
                                var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                                async Task Wait()
                                {
                                    context.Logger?.LogInformation("Waiting for {Resource}.", a.Resource.Name);

                                    try
                                    {
                                        await tcs.Task;

                                        context.Logger?.LogInformation("Waiting for {Resource} completed.", a.Resource.Name);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        context.Logger?.LogError("Waiting for {Resource} is failed.", a.Resource.Name);
                                    }
                                    catch (Exception ex)
                                    {
                                        context.Logger?.LogError(ex, "Waiting for {Resource} is failed.", a.Resource.Name);
                                    }
                                }

                                pendingAnnotations[a] = tcs;

                                dependencies.Add(Wait());
                            }
                        }
                    }

                    await Task.WhenAll(dependencies).WaitAsync(context.CancellationToken);
                }));
            }

            _ = Task.Run(async () =>
           {
               var stoppingToken = _cts.Token;

               // These states are terminal but we need a better way to detect that
               static bool IsKnownTerminalState(CustomResourceSnapshot snapshot) =>
                   snapshot.State == "FailedToStart" ||
                   snapshot.State == "Exited" ||
                   snapshot.ExitCode is not null;

               // Watch for global resource state changes
               await foreach (var resourceEvent in resourceNotificationService.WatchAsync(stoppingToken))
               {
                   if (waitingResources.TryGetValue(resourceEvent.Resource, out var pendingAnnotations))
                   {
                       foreach (var (waitOn, tcs) in pendingAnnotations)
                       {
                           if (waitOn.States is string[] states && states.Contains(resourceEvent.Snapshot.State?.Text, StringComparer.Ordinal))
                           {
                               pendingAnnotations.TryRemove(waitOn, out _);

                               _ = DoTheHealthCheck(resourceEvent, tcs);
                           }
                           else if (waitOn.WaitUntilCompleted)
                           {
                               if (IsKnownTerminalState(resourceEvent.Snapshot))
                               {
                                   pendingAnnotations.TryRemove(waitOn, out _);

                                   _ = DoTheHealthCheck(resourceEvent, tcs);
                               }
                           }
                           else if (waitOn.States is null)
                           {
                               if (resourceEvent.Snapshot.State == "Running")
                               {
                                   pendingAnnotations.TryRemove(waitOn, out _);

                                   _ = DoTheHealthCheck(resourceEvent, tcs);
                               }
                               else if (IsKnownTerminalState(resourceEvent.Snapshot))
                               {
                                   pendingAnnotations.TryRemove(waitOn, out _);

                                   tcs.TrySetCanceled();
                               }
                           }
                       }
                   }
               }
           },
           cancellationToken);

            return Task.CompletedTask;
        }

        private async Task DoTheHealthCheck(ResourceEvent resourceEvent, TaskCompletionSource tcs)
        {
            var url = resourceEvent.Snapshot.Urls.Select(u => new Uri(u.Url)).FirstOrDefault();

            resourceEvent.Resource.TryGetLastAnnotation<HealthCheckAnnotation>(out var healthCheckAnnotation);

            Func<CancellationToken, ValueTask>? operation = (url?.Scheme, healthCheckAnnotation?.HealthCheckFactory) switch
            {
                ("http" or "https", null) => async (ct) =>
                {
                    // For an HTTP resource, see if we can make a request to the endpoint
                    using var client = new HttpClient();
                    var response = await client.GetAsync(url, ct);
                }
                ,
                (_, Func<string, IHealthCheck> factory) => async (ct) =>
                {
                    if (resourceEvent.Resource is not IResourceWithConnectionString c)
                    {
                        return;
                    }

                    // Get the connection string from the resource so we can create the health check
                    // with the correct connection information

                    // TODO: We could cache this lookup
                    var cs = await c.GetConnectionStringAsync(ct);

                    if (cs is null)
                    {
                        return;
                    }

                    var check = factory(cs);

                    var context = new HealthCheckContext()
                    {
                        Registration = new HealthCheckRegistration("", check, HealthStatus.Unhealthy, [])
                    };

                    var result = await check.CheckHealthAsync(context, ct);

                    if (result.Exception is not null)
                    {
                        ExceptionDispatchInfo.Throw(result.Exception);
                    }

                    if (result.Status != HealthStatus.Healthy)
                    {
                        throw new Exception("Health check failed");
                    }
                }
                ,
                _ => null,
            };

            try
            {
                if (operation is not null)
                {
                    await ResiliencePipeline.ExecuteAsync(operation);
                }

                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }

        private ResiliencePipeline? _resiliencePipeline;
        private ResiliencePipeline ResiliencePipeline => _resiliencePipeline ??= CreateResiliencyPipeline();

        private static ResiliencePipeline CreateResiliencyPipeline()
        {
            var retryUntilCancelled = new RetryStrategyOptions()
            {
                ShouldHandle = new PredicateBuilder().HandleInner<Exception>(),
                BackoffType = DelayBackoffType.Exponential,
                MaxRetryAttempts = 5,
                UseJitter = true,
                MaxDelay = TimeSpan.FromSeconds(30)
            };

            return new ResiliencePipelineBuilder().AddRetry(retryUntilCancelled).Build();
        }

        public ValueTask DisposeAsync()
        {
            _cts.Cancel();
            return default;
        }
    }
}