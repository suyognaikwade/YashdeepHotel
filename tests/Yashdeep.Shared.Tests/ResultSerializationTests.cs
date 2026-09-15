using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using Yashdeep.Shared.Results;
using Yashdeep.Shared.Serialization;

namespace Yashdeep.Shared.Tests;

public class ResultSerializationTests
{
    [Fact]
    public void Result_Success_ShouldHaveNoErrors()
    {
        var result = Result.Success("Order created successfully");
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("Order created successfully", result.Value);
    }

    [Fact]
    public void Result_Failure_AccessingValue_ShouldThrowInvalidOperationException()
    {
        var error = Error.NotFound("Order.NotFound", "Order with specified ID was not found.");
        var result = Result.Failure<string>(error);

        Assert.True(result.IsFailure);
        Assert.Throws<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void Error_Validation_Serialization_ShouldPreserveDetails()
    {
        var options = YashdeepJsonSerializerOptions.Default;
        var details = new Dictionary<string, string[]>
        {
            { "Quantity", new[] { "Quantity must be greater than zero." } }
        };

        var error = Error.Validation("Order.InvalidQuantity", "Validation failed for order item.", details);

        var json = JsonSerializer.Serialize(error, options);
        var deserializedError = JsonSerializer.Deserialize<Error>(json, options);

        Assert.Equal(error.Code, deserializedError.Code);
        Assert.Equal(error.Type, deserializedError.Type);
        Assert.NotNull(deserializedError.Details);
        Assert.True(deserializedError.Details.ContainsKey("Quantity"));
    }
}
