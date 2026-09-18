using System.Text.Json;
using Xunit;
using Yashdeep.Shared.Contracts;
using Yashdeep.Shared.Results;
using Yashdeep.Shared.Serialization;

namespace Yashdeep.Shared.Tests;

public class ApiContractTests
{
    [Fact]
    public void ApiResponse_Ok_ShouldSerializeStandardEnvelope()
    {
        var options = YashdeepJsonSerializerOptions.Default;
        var response = ApiResponse<string>.Ok("Success Payload", "corr-1234");

        var json = JsonSerializer.Serialize(response, options);
        Assert.Contains("\"success\":true", json);
        Assert.Contains("\"data\":\"Success Payload\"", json);
        Assert.Contains("\"correlationId\":\"corr-1234\"", json);
    }

    [Fact]
    public void ApiResponse_Fail_ShouldSerializeErrorEnvelope()
    {
        var options = YashdeepJsonSerializerOptions.Default;
        var error = Error.NotFound("Item.NotFound", "Inventory item not found.");
        var response = ApiResponse<string>.Fail(error, "corr-9999");

        var json = JsonSerializer.Serialize(response, options);
        var deserialized = JsonSerializer.Deserialize<ApiResponse<string>>(json, options);

        Assert.NotNull(deserialized);
        Assert.False(deserialized.Success);
        Assert.NotNull(deserialized.Error);
        Assert.Equal("Item.NotFound", deserialized.Error.Code);
        Assert.Equal(404, deserialized.Error.NumericCode);
    }

    [Fact]
    public void PagedResponse_ShouldCalculateTotalPagesCorrectly()
    {
        var items = new[] { 1, 2, 3, 4, 5 };
        var paged = new PagedResponse<int>(items, pageNumber: 2, pageSize: 5, totalCount: 23);

        Assert.Equal(5, paged.TotalPages);
        Assert.True(paged.HasPreviousPage);
        Assert.True(paged.HasNextPage);
    }
}
