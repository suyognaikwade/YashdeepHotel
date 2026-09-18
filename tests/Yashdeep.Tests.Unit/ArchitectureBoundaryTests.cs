using System.Reflection;
using Xunit;

namespace Yashdeep.Tests.Unit;

public class ArchitectureBoundaryTests
{
    [Fact]
    public void DomainAssembly_HasNoDependenciesOnInfrastructureOrFrameworks()
    {
        var domainAssembly = Assembly.GetAssembly(typeof(Yashdeep.Domain.Entities.Tenant));
        Assert.NotNull(domainAssembly);

        var referencedAssemblies = domainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        // Assert Domain does NOT depend on EF Core, ASP.NET Core, HTTP, or Persistence
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", referencedAssemblies);
        Assert.DoesNotContain("Microsoft.AspNetCore", referencedAssemblies);
        Assert.DoesNotContain("System.Net.Http", referencedAssemblies);
        Assert.DoesNotContain("Yashdeep.Persistence.Local", referencedAssemblies);
        Assert.DoesNotContain("Yashdeep.Persistence.Cloud", referencedAssemblies);
        Assert.DoesNotContain("Yashdeep.Infrastructure", referencedAssemblies);
    }

    [Fact]
    public void ApplicationAssembly_DoesNotDependOnConcretePersistenceOrInfrastructure()
    {
        var appAssembly = Assembly.GetAssembly(typeof(Yashdeep.Application.Pos.Workflows.CompletePosWorkflowUseCase));
        Assert.NotNull(appAssembly);

        var referencedAssemblies = appAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        // Assert Application does NOT depend on concrete persistence or infrastructure assemblies
        Assert.DoesNotContain("Yashdeep.Persistence.Local", referencedAssemblies);
        Assert.DoesNotContain("Yashdeep.Persistence.Cloud", referencedAssemblies);
        Assert.DoesNotContain("Yashdeep.Infrastructure", referencedAssemblies);
    }

    [Fact]
    public void OutboxMessage_IsCanonicalAggregateInDomainOutboxNamespace()
    {
        var outboxType = typeof(Yashdeep.Domain.Outbox.OutboxMessage);
        Assert.NotNull(outboxType);
        Assert.Equal("Yashdeep.Domain.Outbox", outboxType.Namespace);

        // Verify duplicate Sync.OutboxMessage does not exist
        var domainAssembly = Assembly.GetAssembly(outboxType);
        var syncOutboxType = domainAssembly!.GetType("Yashdeep.Domain.Entities.Sync.OutboxMessage");
        Assert.Null(syncOutboxType);
    }

    [Fact]
    public void ConnectivityState_IsCanonicalEnumInSharedConnectivityNamespace()
    {
        var connectivityStateType = typeof(Yashdeep.Shared.Connectivity.ConnectivityState);
        Assert.NotNull(connectivityStateType);
        Assert.True(connectivityStateType.IsEnum);
        Assert.Equal("Yashdeep.Shared.Connectivity", connectivityStateType.Namespace);

        // Verify duplicate Domain.Connectivity.ConnectivityState enum does not exist
        var domainAssembly = Assembly.GetAssembly(typeof(Yashdeep.Domain.Entities.Tenant));
        var duplicateEnum = domainAssembly!.GetTypes()
            .FirstOrDefault(t => t.Name == "ConnectivityState" && t.IsEnum);
        Assert.Null(duplicateEnum);
    }
}
