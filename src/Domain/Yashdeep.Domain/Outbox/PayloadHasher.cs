using System.Security.Cryptography;
using System.Text;

namespace Yashdeep.Domain.Outbox;

public static class PayloadHasher
{
    public static string ComputeSha256Hash(string payloadJson)
    {
        if (payloadJson is null)
        {
            throw new ArgumentNullException(nameof(payloadJson));
        }

        byte[] bytes = Encoding.UTF8.GetBytes(payloadJson);
        byte[] hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
