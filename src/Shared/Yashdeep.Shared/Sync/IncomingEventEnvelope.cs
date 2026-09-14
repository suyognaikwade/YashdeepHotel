using System;
using System.Security.Cryptography;
using System.Text;

namespace Yashdeep.Shared.Sync
{
    public sealed class IncomingEventEnvelope
    {
        public Guid EventId { get; set; }
        public string EventType { get; set; } = string.Empty;
        public int EventVersion { get; set; } = 1;
        public string AggregateType { get; set; } = string.Empty;
        public Guid AggregateId { get; set; }
        public Guid TenantId { get; set; }
        public Guid BranchId { get; set; }
        public Guid DeviceId { get; set; }
        public long SequenceNumber { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string PayloadJson { get; set; } = "{}";
        public string? PayloadHash { get; set; }
        public string? PayloadSignature { get; set; }

        public string ComputePayloadHash()
        {
            if (string.IsNullOrEmpty(PayloadJson))
            {
                return PayloadHasher.ComputeSha256("{}");
            }
            return PayloadHasher.ComputeSha256(PayloadJson);
        }
    }

    public static class PayloadHasher
    {
        public static string ComputeSha256(string content)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
            byte[] hash = SHA256.HashData(bytes);
            return Convert.ToHexStringLower(hash);
        }
    }
}
