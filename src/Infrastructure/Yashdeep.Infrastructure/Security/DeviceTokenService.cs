using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Yashdeep.Shared.Security;

namespace Yashdeep.Infrastructure.Security;

public class DeviceTokenService : IDeviceTokenService
{
    private readonly byte[] _signingKey;

    public DeviceTokenService(string? signingSecret = null)
    {
        var secret = signingSecret ?? "Yashdeep-Secret-Device-Signing-Key-32BytesLongMinimum!!";
        _signingKey = Encoding.UTF8.GetBytes(secret);
    }

    public string IssueToken(DeviceTokenPayload payload)
    {
        var json = JsonSerializer.Serialize(payload);
        var payloadBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        using var hmac = new HMACSHA256(_signingKey);
        var signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        var signatureBase64 = Convert.ToBase64String(signatureBytes);

        return $"{payloadBase64}.{signatureBase64}";
    }

    public DeviceTokenResult ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new DeviceTokenResult(false, token, null, "Token is empty.");
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return new DeviceTokenResult(false, token, null, "Malformed token format.");
        }

        var payloadBase64 = parts[0];
        var signatureBase64 = parts[1];

        using var hmac = new HMACSHA256(_signingKey);
        var expectedSignatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBase64));
        var expectedSignatureBase64 = Convert.ToBase64String(expectedSignatureBytes);

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signatureBase64),
                Encoding.UTF8.GetBytes(expectedSignatureBase64)))
        {
            return new DeviceTokenResult(false, token, null, "Invalid token signature.");
        }

        try
        {
            var jsonBytes = Convert.FromBase64String(payloadBase64);
            var json = Encoding.UTF8.GetString(jsonBytes);
            var payload = JsonSerializer.Deserialize<DeviceTokenPayload>(json);

            if (payload == null)
            {
                return new DeviceTokenResult(false, token, null, "Failed to deserialize token payload.");
            }

            if (DateTime.UtcNow > payload.ExpiresAtUtc)
            {
                return new DeviceTokenResult(false, token, payload, "Token has expired.");
            }

            return new DeviceTokenResult(true, token, payload, null);
        }
        catch (Exception ex)
        {
            return new DeviceTokenResult(false, token, null, $"Payload parsing error: {ex.Message}");
        }
    }
}
