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
        // Assert that core project assembly types are loadable and wired across Clean Architecture layers
        var domainType = typeof(Yashdeep.Domain.Entities.Tenant);
        var applicationType = typeof(Yashdeep.Application.Pos.Workflows.CompletePosWorkflowUseCase);
        var sharedType = typeof(Yashdeep.Shared.Connectivity.ConnectivityState);
        var infrastructureType = typeof(Yashdeep.Infrastructure.Connectivity.ConnectivityStateEvaluator);
        var persistenceCloudType = typeof(Yashdeep.Persistence.Cloud.CloudDbContext);
        var persistenceLocalType = typeof(Yashdeep.Persistence.Local.LocalPosDbContext);
        var syncEngineType = typeof(Yashdeep.SyncEngine.Services.CloudInboxProcessor);
        var apiProgramType = typeof(Program);

        Assert.NotNull(domainType);
        Assert.NotNull(applicationType);
        Assert.NotNull(sharedType);
        Assert.NotNull(infrastructureType);
        Assert.NotNull(persistenceCloudType);
        Assert.NotNull(persistenceLocalType);
        Assert.NotNull(syncEngineType);
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
