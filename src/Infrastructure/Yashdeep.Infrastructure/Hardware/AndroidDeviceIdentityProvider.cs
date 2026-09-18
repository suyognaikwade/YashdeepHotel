using Yashdeep.Shared.Hardware;

namespace Yashdeep.Infrastructure.Hardware;

public class AndroidDeviceIdentityProvider : IDeviceIdentityProvider
{
    private readonly DeviceHardwareInfo _info;

    public string PlatformName => "Android";

    public AndroidDeviceIdentityProvider(
        string? androidId = null,
        string? buildSerial = null,
        string? storageUuid = null,
        string? macAddress = null,
        string? deviceModel = null)
    {
        _info = new DeviceHardwareInfo(
            SystemUuid: androidId ?? "ANDROID-ID-8F92A10C99B871D2",
            CpuId: buildSerial ?? "ARM-CORTEX-A78-OCTACORE",
            VolumeSerial: storageUuid ?? "EXT4-INTERNAL-STORAGE-UUID-771B",
            PrimaryMacAddress: macAddress ?? "02:00:00:00:00:00", // Android randomized MAC
            PlatformName: PlatformName,
            HostName: deviceModel ?? "POS-TAB-WAITER-01");
    }

    public DeviceHardwareInfo GetHardwareInfo() => _info;

    public string GetHardwareFingerprint() => _info.GenerateFingerprint();

    public double CalculateDriftScore(string existingFingerprint, DeviceHardwareInfo currentInfo)
    {
        var currentFingerprint = currentInfo.GenerateFingerprint();
        return string.Equals(existingFingerprint, currentFingerprint, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.5;
    }
}
