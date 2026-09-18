using System;
using System.Text.Json;
using Xunit;
using Yashdeep.Shared.Identifiers;
using Yashdeep.Shared.Serialization;

namespace Yashdeep.Shared.Tests;

public class IdentifierTests
{
    [Fact]
    public void TenantId_New_ShouldGenerateNonEmptyGuid()
    {
        var tenantId = TenantId.New();
        Assert.NotEqual(Guid.Empty, tenantId.Value);
        Assert.False(tenantId.IsEmpty);
    }

    [Fact]
    public void Identifier_ToString_ShouldReturnFormattedGuidString()
    {
        var guid = Guid.Parse("018f3a5b-7c9d-7000-8000-000000000001");
        var branchId = BranchId.FromGuid(guid);

        Assert.Equal("018f3a5b-7c9d-7000-8000-000000000001", branchId.ToString());
    }

    [Fact]
    public void Identifier_SerializationAndDeserialization_ShouldBeLossless()
    {
        var options = YashdeepJsonSerializerOptions.Default;
        var originalId = DeviceId.New();

        var json = JsonSerializer.Serialize(originalId, options);
        Assert.Equal($"\"{originalId.Value:D}\"", json);

        var deserializedId = JsonSerializer.Deserialize<DeviceId>(json, options);
        Assert.Equal(originalId, deserializedId);
    }

    [Fact]
    public void Identifier_TryParse_WithValidString_ShouldSucceed()
    {
        var guidStr = "018f3a5b-7c9d-7000-8000-000000000001";
        var success = UserId.TryParse(guidStr, out var userId);

        Assert.True(success);
        Assert.Equal(Guid.Parse(guidStr), userId.Value);
    }

    [Fact]
    public void Identifier_TryParse_WithInvalidString_ShouldReturnEmpty()
    {
        var success = UserId.TryParse("invalid-guid", out var userId);

        Assert.False(success);
        Assert.Equal(UserId.Empty, userId);
    }
}
