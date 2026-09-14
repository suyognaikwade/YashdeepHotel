using Yashdeep.Shared.Hardware;

namespace Yashdeep.Infrastructure.Hardware;

public class WindowsDeviceIdentityProvider : IDeviceIdentityProvider
{
    private readonly DeviceHardwareInfo _info;

    public string PlatformName => "Windows";

    public WindowsDeviceIdentityProvider(
        string? systemUuid = null,
        string? cpuId = null,
        string? volumeSerial = null,
        string? macAddress = null,
        string? hostName = null)
    {
        _info = new DeviceHardwareInfo(
            SystemUuid: systemUuid ?? "WIN-BIOS-UUID-984F-221A",
            CpuId: cpuId ?? "CPU-INTEL-CORE-I7-10700K",
            VolumeSerial: volumeSerial ?? "VOL-C-DRIVE-UUID-88A9",
            PrimaryMacAddress: macAddress ?? "00:1A:2B:3C:4D:5E",
            PlatformName: PlatformName,
            HostName: hostName ?? "POS-WIN-TERM1");
    }

    public DeviceHardwareInfo GetHardwareInfo() => _info;

    public string GetHardwareFingerprint() => _info.GenerateFingerprint();

    public double CalculateDriftScore(string existingFingerprint, DeviceHardwareInfo currentInfo)
    {
        var currentFingerprint = currentInfo.GenerateFingerprint();
        if (string.Equals(existingFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        // Calculate attribute match ratio
        int matchCount = 0;
        int totalAttributes = 4;

        if (existingFingerprint.Contains(currentInfo.SystemUuid[..Math.Min(4, currentInfo.SystemUuid.Length)])) matchCount++;
        if (existingFingerprint.Contains(currentInfo.CpuId[..Math.Min(4, currentInfo.CpuId.Length)])) matchCount++;
        if (existingFingerprint.Contains(currentInfo.VolumeSerial[..Math.Min(4, currentInfo.VolumeSerial.Length)])) matchCount++;
        if (existingFingerprint.Contains(currentInfo.PrimaryMacAddress[..Math.Min(4, currentInfo.PrimaryMacAddress.Length)])) matchCount++;

        return (double)matchCount / totalAttributes;
    }
}
