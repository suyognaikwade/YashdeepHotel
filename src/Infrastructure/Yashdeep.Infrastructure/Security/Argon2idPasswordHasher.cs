using System.Security.Cryptography;
using System.Text;
using Isopoh.Cryptography.Argon2;
using Yashdeep.Application.Interfaces;
using Yashdeep.Domain.Entities;

namespace Yashdeep.Infrastructure.Security;

/// <summary>
/// Production password hasher implementing Argon2id as required by IMPLEMENTATION_CONTRACT.md section 12.1 and SECURITY_ARCHITECTURE.md section 2.1.
/// Explicitly rejects bcrypt, PBKDF2, or fallback hashes.
/// </summary>
public class Argon2idPasswordHasher : IPasswordHasher
{
    public string HashPassword(User user, string password)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(password))
            throw new ArgumentException("Password cannot be null or empty.", nameof(password));

        byte[] salt = RandomNumberGenerator.GetBytes(16);
        var config = new Argon2Config
        {
            Type = Argon2Type.HybridAddressing, // Argon2id (hybrid of Argon2d and Argon2i)
            MemoryCost = 65536, // 64MB
            TimeCost = 3,
            Lanes = 4,
            Threads = 4,
            Password = Encoding.UTF8.GetBytes(password),
            Salt = salt
        };

        using var argon2 = new Argon2(config);
        using var hash = argon2.Hash();
        return config.EncodeString(hash.Buffer);
    }

    public bool VerifyPassword(User user, string hashedPassword, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(user);
        if (string.IsNullOrEmpty(hashedPassword) || string.IsNullOrEmpty(providedPassword))
            return false;

        try
        {
            return Argon2.Verify(hashedPassword, providedPassword);
        }
        catch
        {
            return false;
        }
    }
}
