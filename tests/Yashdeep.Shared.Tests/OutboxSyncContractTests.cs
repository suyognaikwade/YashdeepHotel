using System;
using System.Text.Json;
using Xunit;
using Yashdeep.Shared.Identifiers;
using Yashdeep.Shared.Serialization;
using Yashdeep.Shared.Sync;

namespace Yashdeep.Shared.Tests;

public class OutboxSyncContractTests
{
    [Fact]
    public void OutboxEventPayload_Serialization_ShouldContainAllRequiredFields()
    {
        var options = YashdeepJsonSerializerOptions.Default;
        var payload = new OutboxEventPayload
        {
            EventId = Guid.Parse("018f3a5b-7c9d-7000-8000-000000000001"),
            EventType = "OrderPlacedEvent",
            AggregateType = "Order",
            AggregateId = AggregateId.Parse("018f3a5b-7c9d-7000-8000-000000000099"),
            TenantId = TenantId.Parse("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
            BranchId = BranchId.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7"),
            DeviceId = DeviceId.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
            SequenceNumber = 1042,
            EventVersion = "1.0",
            CreatedUtc = new DateTime(2026, 9, 14, 1, 15, 0, DateTimeKind.Utc),
            PayloadJson = "{\"tableNo\":5,\"amount\":1250.00}",
            PayloadSignature = "Ed25519_Signature_Hex_Sample"
        };

        var json = JsonSerializer.Serialize(payload, options);
        var deserialized = JsonSerializer.Deserialize<OutboxEventPayload>(json, options);

        Assert.NotNull(deserialized);
        Assert.Equal(payload.EventId, deserialized.EventId);
        Assert.Equal(payload.EventType, deserialized.EventType);
        Assert.Equal(payload.AggregateType, deserialized.AggregateType);
        Assert.Equal(payload.AggregateId, deserialized.AggregateId);
        Assert.Equal(payload.TenantId, deserialized.TenantId);
        Assert.Equal(payload.BranchId, deserialized.BranchId);
        Assert.Equal(payload.DeviceId, deserialized.DeviceId);
        Assert.Equal(payload.SequenceNumber, deserialized.SequenceNumber);
        Assert.Equal(payload.CreatedUtc, deserialized.CreatedUtc);
        Assert.Equal(payload.PayloadJson, deserialized.PayloadJson);
        Assert.Equal(payload.PayloadSignature, deserialized.PayloadSignature);
    }

    [Fact]
    public void SyncBatchRequest_And_Response_ShouldBeSerializable()
    {
        var options = YashdeepJsonSerializerOptions.Default;
        var request = new SyncBatchRequest
        {
            DeviceId = DeviceId.New(),
            TenantId = TenantId.New(),
            BranchId = BranchId.New(),
            BatchId = "batch-001"
        };
        request.Events.Add(new OutboxEventPayload { EventId = Guid.NewGuid(), EventType = "KOTCreated" });

        var jsonReq = JsonSerializer.Serialize(request, options);
        var deserializedReq = JsonSerializer.Deserialize<SyncBatchRequest>(jsonReq, options);

        Assert.NotNull(deserializedReq);
        Assert.Equal("batch-001", deserializedReq.BatchId);
        Assert.Single(deserializedReq.Events);

        var response = new SyncBatchResponse
        {
            BatchId = "batch-001",
            Acknowledgments = new System.Collections.Generic.List<SyncAckItem>
            {
                new() { EventId = request.Events[0].EventId, SequenceNumber = 1, Status = SyncAckStatus.Success }
            }
        };

        var jsonRes = JsonSerializer.Serialize(response, options);
        var deserializedRes = JsonSerializer.Deserialize<SyncBatchResponse>(jsonRes, options);

        Assert.NotNull(deserializedRes);
        Assert.True(deserializedRes.AllSucceeded);
    }
}
