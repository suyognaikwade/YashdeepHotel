using System;
using System.Collections.Generic;
using Yashdeep.Application.Interfaces;
using Yashdeep.Infrastructure.Security;
using Yashdeep.Shared.Entitlements;
using Yashdeep.Shared.Security;
using Xunit;

namespace Yashdeep.Tests;

public class SecurityAuthenticationTests
{
    [Fact]
    public void Argon2idPasswordHasher_HashesAndVerifiesPasswordSuccessfully()
    {
        IPasswordHasher hasher = new Argon2idPasswordHasher();
        string rawPassword = "StrongPassword123!";

        string hashedPassword = hasher.HashPassword(rawPassword);

        Assert.NotNull(hashedPassword);
        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=4$", hashedPassword);
        Assert.False(hashedPassword.Contains(rawPassword, StringComparison.OrdinalIgnoreCase));

        bool isValid = hasher.VerifyPassword(rawPassword, hashedPassword);
        Assert.True(isValid);

        bool isInvalid = hasher.VerifyPassword("WrongPassword123!", hashedPassword);
        Assert.False(isInvalid);
    }

    [Fact]
    public void Argon2idPasswordHasher_TamperedHash_FailsVerification()
    {
        IPasswordHasher hasher = new Argon2idPasswordHasher();
        string rawPassword = "StrongPassword123!";
        string hashedPassword = hasher.HashPassword(rawPassword);

        string tamperedHash = hashedPassword.Substring(0, hashedPassword.Length - 4) + "AAAA";

        bool isValid = hasher.VerifyPassword(rawPassword, tamperedHash);
        Assert.False(isValid);
    }

    [Fact]
    public void DeviceTokenService_ValidatesLegitimateTokenAndRejectsForgedOrExpiredToken()
    {
        var tokenService = new DeviceTokenService("Test-Signing-Secret-32BytesLong!!");
        var tenantId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        var validPayload = new DeviceTokenPayload(
            deviceId,
            tenantId,
            Guid.NewGuid(),
            branchId,
            null,
            null,
            "HW-FINGERPRINT-123",
            "Active",
            DateTime.UtcNow.AddMinutes(-5),
            DateTime.UtcNow.AddDays(7));

        string validToken = tokenService.IssueToken(validPayload);
        var validResult = tokenService.ValidateToken(validToken);

        Assert.True(validResult.IsValid);
        Assert.NotNull(validResult.Payload);
        Assert.Equal(tenantId, validResult.Payload.TenantId);
        Assert.Equal(branchId, validResult.Payload.BranchId);

        // Forged signature
        string forgedToken = validToken.Substring(0, validToken.LastIndexOf('.') + 1) + "ForgedSignatureHash=";
        var forgedResult = tokenService.ValidateToken(forgedToken);
        Assert.False(forgedResult.IsValid);
        Assert.Equal("Invalid token signature.", forgedResult.ErrorReason);

        // Expired token
        var expiredPayload = validPayload with { ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        string expiredToken = tokenService.IssueToken(expiredPayload);
        var expiredResult = tokenService.ValidateToken(expiredToken);
        Assert.False(expiredResult.IsValid);
        Assert.Equal("Token has expired.", expiredResult.ErrorReason);

        // Malformed token
        var malformedResult = tokenService.ValidateToken("Invalid.Token.Format.Parts");
        Assert.False(malformedResult.IsValid);
        Assert.Equal("Malformed token format.", malformedResult.ErrorReason);
    }

    [Fact]
    public void Ed25519EntitlementTokenService_RejectsForgedSignatures()
    {
        var service = new Ed25519EntitlementTokenService();
        var (privateKey, publicKey) = service.GenerateKeyPair();

        var payload = new SignedEntitlementTokenPayload
        {
            TenantId = Guid.NewGuid().ToString(),
            IssuedAtUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ExpirationUnix = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds(),
            Capabilities = new HashSet<string> { "POS_ORDER_ENTRY" }
        };

        var envelope = service.SignTokenPayload(payload, privateKey);
        bool isValid = service.VerifySignature(envelope);
        Assert.True(isValid);

        // Forged payload
        var forgedEnvelope = new SignedEntitlementEnvelope
        {
            PayloadJson = envelope.PayloadJson.Replace("POS_ORDER_ENTRY", "SUPER_ADMIN_PRIVILEGE"),
            SignatureHex = envelope.SignatureHex,
            PublicKeyHex = envelope.PublicKeyHex
        };

        bool isForgedValid = service.VerifySignature(forgedEnvelope);
        Assert.False(isForgedValid);
    }
}
