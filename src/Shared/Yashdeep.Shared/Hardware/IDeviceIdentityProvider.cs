namespace Yashdeep.Shared.Hardware;

public record DeviceHardwareInfo(
    string SystemUuid,
    string CpuId,
    string VolumeSerial,
    string PrimaryMacAddress,
    string PlatformName,
    string HostName)
{
    public string GenerateFingerprint()
    {
        // Must NOT rely on MAC address alone.
        // Combines System UUID, CPU ID, Storage Volume Serial, and Platform Name.
        var rawString = $"{PlatformName.ToUpperInvariant()}|{SystemUuid}|{CpuId}|{VolumeSerial}|{PrimaryMacAddress}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawString);
        var hash = sha256.ComputeHash(bytes);
        var hex = Convert.ToHexString(hash);
        var prefix = PlatformName.StartsWith("Win", StringComparison.OrdinalIgnoreCase) ? "HW-WIN" :
                     PlatformName.StartsWith("Andr", StringComparison.OrdinalIgnoreCase) ? "HW-AND" : "HW-GEN";
        return $"{prefix}-{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
    }
}

public interface IDeviceIdentityProvider
{
    string PlatformName { get; }
    DeviceHardwareInfo GetHardwareInfo();
    string GetHardwareFingerprint();
    double CalculateDriftScore(string existingFingerprint, DeviceHardwareInfo currentInfo);
}
