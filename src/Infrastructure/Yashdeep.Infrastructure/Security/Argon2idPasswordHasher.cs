using System;
using System.Security.Cryptography;
using System.Text;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Yashdeep.Application.Interfaces;

namespace Yashdeep.Infrastructure.Security;

public class Argon2idPasswordHasher : IPasswordHasher
{
    private const int MemoryKb = 65536; // 64 MB
    private const int Iterations = 3;
    private const int Parallelism = 4;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public string HashPassword(string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("Password cannot be empty.", nameof(password));
        }

        byte[] salt = new byte[SaltSize];
        RandomNumberGenerator.Fill(salt);

        byte[] hash = GenerateHash(password, salt, MemoryKb, Iterations, Parallelism, HashSize);

        string saltBase64 = Convert.ToBase64String(salt);
        string hashBase64 = Convert.ToBase64String(hash);

        return $"$argon2id$v=19$m={MemoryKb},t={Iterations},p={Parallelism}${saltBase64}${hashBase64}";
    }

    public bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
        {
            return false;
        }

        var parts = hashedPassword.Split('$');
        if (parts.Length != 6 || parts[1] != "argon2id")
        {
            return false;
        }

        try
        {
            var paramParts = parts[3].Split(',');
            int memoryKb = int.Parse(paramParts[0].Substring(2));
            int iterations = int.Parse(paramParts[1].Substring(2));
            int parallelism = int.Parse(paramParts[2].Substring(2));

            byte[] salt = Convert.FromBase64String(parts[4]);
            byte[] expectedHash = Convert.FromBase64String(parts[5]);

            byte[] actualHash = GenerateHash(password, salt, memoryKb, iterations, parallelism, expectedHash.Length);

            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static byte[] GenerateHash(string password, byte[] salt, int memoryKb, int iterations, int parallelism, int outputLength)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);

        var builder = new Argon2Parameters.Builder(Argon2Parameters.Argon2id)
            .WithVersion(19) // Argon2 v1.3 is version 0x13 (19 in decimal)
            .WithMemoryAsKB(memoryKb)
            .WithIterations(iterations)
            .WithParallelism(parallelism)
            .WithSalt(salt);

        var generator = new Argon2BytesGenerator();
        generator.Init(builder.Build());

        byte[] result = new byte[outputLength];
        generator.GenerateBytes(passwordBytes, result, 0, result.Length);

        return result;
    }
}
