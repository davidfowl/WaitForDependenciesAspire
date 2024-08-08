using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using TestExample.Support;

namespace TestExample.Tests;

[Collection(nameof(AspireFixtureCollection))]
public class ApiExampleTests(AspireAppHostFixture fixture)
{
    private readonly DistributedApplication _distributedApplication = fixture.DistributedApplicationInstance;
    
    [Fact]
    public async Task ProductsApi_GetProducts_ReturnsAtLeastOne()
    {
        using var httpClient = _distributedApplication.CreateHttpClient("api");
        var response = await httpClient.GetAsync("/");
        
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<Product>>();
        
        body.Should().NotBeNullOrEmpty();
        body!.Count.Should().BeGreaterThan(0);
    }
    
    [Fact]
    public async Task GenericApi_GetIndex_ReturnsHelloWorld()
    {
        using var httpClient = _distributedApplication.CreateHttpClient("api0");
        var response = await httpClient.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        
        body.Should().Be("Hello World!");
    }
}