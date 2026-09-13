namespace Yashdeep.Infrastructure.Security;

using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;
using Yashdeep.Application.Entitlements;
using Yashdeep.Shared.Entitlements;

public interface IEntitlementTokenSigner
{
    SignedEntitlementEnvelope SignTokenPayload(SignedEntitlementTokenPayload payload, Ed25519PrivateKeyParameters privateKey);
    (Ed25519PrivateKeyParameters PrivateKey, Ed25519PublicKeyParameters PublicKey) GenerateKeyPair();
}

public interface IEntitlementTokenValidator : IEntitlementTokenVerifier
{
    bool VerifySignature(SignedEntitlementEnvelope envelope, string expectedPublicKeyHex);
}

public sealed class Ed25519EntitlementTokenService : IEntitlementTokenValidator, IEntitlementTokenSigner
{
    private readonly string? _defaultPublicKeyHex;

    public Ed25519EntitlementTokenService(string? defaultPublicKeyHex = null)
    {
        _defaultPublicKeyHex = defaultPublicKeyHex;
    }

    public (Ed25519PrivateKeyParameters PrivateKey, Ed25519PublicKeyParameters PublicKey) GenerateKeyPair()
    {
        var random = new SecureRandom();
        var keyGen = new Ed25519KeyPairGenerator();
        keyGen.Init(new Ed25519KeyGenerationParameters(random));
        var pair = keyGen.GenerateKeyPair();
        return ((Ed25519PrivateKeyParameters)pair.Private, (Ed25519PublicKeyParameters)pair.Public);
    }

    public SignedEntitlementEnvelope SignTokenPayload(SignedEntitlementTokenPayload payload, Ed25519PrivateKeyParameters privateKey)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(privateKey);

        var publicKey = privateKey.GeneratePublicKey();
        byte[] pubBytes = publicKey.GetEncoded();
        string publicKeyHex = Convert.ToHexString(pubBytes).ToLowerInvariant();

        string payloadJson = JsonSerializer.Serialize(payload);
        byte[] msgBytes = Encoding.UTF8.GetBytes(payloadJson);

        var signer = new Ed25519Signer();
        signer.Init(true, privateKey);
        signer.BlockUpdate(msgBytes, 0, msgBytes.Length);
        byte[] sigBytes = signer.GenerateSignature();
        string signatureHex = Convert.ToHexString(sigBytes).ToLowerInvariant();

        return new SignedEntitlementEnvelope
        {
            PayloadJson = payloadJson,
            SignatureHex = signatureHex,
            PublicKeyHex = publicKeyHex
        };
    }

    public bool VerifySignature(SignedEntitlementEnvelope envelope)
    {
        if (envelope == null)
        {
            return false;
        }

        string keyToUse = !string.IsNullOrWhiteSpace(envelope.PublicKeyHex)
            ? envelope.PublicKeyHex
            : _defaultPublicKeyHex ?? string.Empty;

        return VerifySignature(envelope, keyToUse);
    }

    public bool VerifySignature(SignedEntitlementEnvelope envelope, string expectedPublicKeyHex)
    {
        if (envelope == null ||
            string.IsNullOrWhiteSpace(envelope.PayloadJson) ||
            string.IsNullOrWhiteSpace(envelope.SignatureHex) ||
            string.IsNullOrWhiteSpace(expectedPublicKeyHex))
        {
            return false;
        }

        try
        {
            byte[] pubKeyBytes = Convert.FromHexString(expectedPublicKeyHex);
            if (pubKeyBytes.Length != Ed25519PublicKeyParameters.KeySize)
            {
                return false;
            }

            byte[] sigBytes = Convert.FromHexString(envelope.SignatureHex);
            if (sigBytes.Length != 64)
            {
                return false;
            }

            byte[] msgBytes = Encoding.UTF8.GetBytes(envelope.PayloadJson);

            var pubKeyParams = new Ed25519PublicKeyParameters(pubKeyBytes, 0);
            var verifier = new Ed25519Signer();
            verifier.Init(false, pubKeyParams);
            verifier.BlockUpdate(msgBytes, 0, msgBytes.Length);

            return verifier.VerifySignature(sigBytes);
        }
        catch (Exception)
        {
            return false;
        }
    }
}
