using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Yashdeep.Tests.Unit;

public class FoundationArchitectureTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public FoundationArchitectureTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public void FoundationSolution_ProjectsAndTypes_AreWiredCorrectly()
    {
        // Assert that core project assembly types are loadable and wired
        var domainType = typeof(Domain.Class1);
        var applicationType = typeof(Application.Class1);
        var sharedType = typeof(Shared.Class1);
        var infrastructureType = typeof(Infrastructure.Class1);
        var persistenceCloudType = typeof(Persistence.Cloud.Class1);
        var persistenceLocalType = typeof(Persistence.Local.Class1);
        var syncEngineType = typeof(SyncEngine.Class1);
        var clientBlazorType = typeof(Client.Blazor.Component1);
        var clientMauiType = typeof(Client.Maui.Class1);
        var apiProgramType = typeof(Program);

        Assert.NotNull(domainType);
        Assert.NotNull(applicationType);
        Assert.NotNull(sharedType);
        Assert.NotNull(infrastructureType);
        Assert.NotNull(persistenceCloudType);
        Assert.NotNull(persistenceLocalType);
        Assert.NotNull(syncEngineType);
        Assert.NotNull(clientBlazorType);
        Assert.NotNull(clientMauiType);
        Assert.NotNull(apiProgramType);
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsHealthyStatus()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/health");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        using var jsonDoc = JsonDocument.Parse(content);
        var status = jsonDoc.RootElement.GetProperty("status").GetString();
        var service = jsonDoc.RootElement.GetProperty("service").GetString();

        Assert.Equal("Healthy", status);
        Assert.Equal("Yashdeep.Server.Api", service);
    }
}
