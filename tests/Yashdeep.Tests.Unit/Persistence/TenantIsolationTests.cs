using Microsoft.EntityFrameworkCore;
using Yashdeep.Application.Context;
using Yashdeep.Domain.Entities;
using Yashdeep.Persistence.Cloud;
using Xunit;
using FluentAssertions;

namespace Yashdeep.Tests.Unit.Persistence;

public class TenantIsolationTests
{
    [Fact]
    public void GlobalQueryFilter_ShouldFilterUsersByTenantId()
    {
        // Arrange
        var tenant1Id = Guid.NewGuid();
        var tenant2Id = Guid.NewGuid();

        var org1Id = Guid.NewGuid();
        var org2Id = Guid.NewGuid();

        var options = new DbContextOptionsBuilder<CloudDbContext>()
            .UseInMemoryDatabase(databaseName: $"TenantTestDb_{Guid.NewGuid()}")
            .Options;

        var tenant1Context = new TenantContext();
        tenant1Context.SetContext(tenant1Id, org1Id, Guid.NewGuid());

        // Seed data using un-filtered context or direct insertion
        using (var seedContext = new CloudDbContext(options, tenant1Context))
        {
            var user1 = new User(Guid.NewGuid(), tenant1Id, org1Id, "tenant1_user", "t1@example.com", "hash1");
            var user2 = new User(Guid.NewGuid(), tenant2Id, org2Id, "tenant2_user", "t2@example.com", "hash2");

            seedContext.Users.AddRange(user1, user2);
            seedContext.SaveChanges();
        }

        // Act & Assert 1: Query as Tenant 1
        using (var db1 = new CloudDbContext(options, tenant1Context))
        {
            var usersTenant1 = db1.Users.ToList();
            usersTenant1.Should().HaveCount(1);
            usersTenant1[0].Username.Should().Be("tenant1_user");
            usersTenant1[0].TenantId.Should().Be(tenant1Id);
        }

        // Act & Assert 2: Query as Tenant 2
        var tenant2Context = new TenantContext();
        tenant2Context.SetContext(tenant2Id, org2Id, Guid.NewGuid());

        using (var db2 = new CloudDbContext(options, tenant2Context))
        {
            var usersTenant2 = db2.Users.ToList();
            usersTenant2.Should().HaveCount(1);
            usersTenant2[0].Username.Should().Be("tenant2_user");
            usersTenant2[0].TenantId.Should().Be(tenant2Id);
        }
    }
}
