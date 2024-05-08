using Projects;

namespace TestExample.Support;

public class AspireAppHostFixture : IAsyncLifetime
{
    public DistributedApplication DistributedApplicationInstance { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<WaitForDependencies_AppHost>();
        DistributedApplicationInstance = await appHost.BuildAsync();
        await DistributedApplicationInstance.StartAsync();
    }
    
    public async Task DisposeAsync()
    {
        await DistributedApplicationInstance.StopAsync();
        await DistributedApplicationInstance.DisposeAsync();
    }
}