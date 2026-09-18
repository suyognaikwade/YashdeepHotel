using System;
using Yashdeep.Shared.Hardware;

namespace Yashdeep.Infrastructure.Hardware;

public class TestDeviceIdentityProvider : IDeviceIdentityProvider
{
    private DeviceHardwareInfo _info;

    public string PlatformName => _info.PlatformName;

    public TestDeviceIdentityProvider(
        string platformName = "TestPlatform",
        string systemUuid = "TEST-UUID-1234",
        string cpuId = "TEST-CPU-5678",
        string volumeSerial = "TEST-VOL-9012",
        string macAddress = "AA:BB:CC:DD:EE:FF",
        string hostName = "TEST-HOST-01")
    {
        _info = new DeviceHardwareInfo(systemUuid, cpuId, volumeSerial, macAddress, platformName, hostName);
    }

    public void UpdateHardwareInfo(DeviceHardwareInfo newInfo)
    {
        _info = newInfo;
    }

    public DeviceHardwareInfo GetHardwareInfo() => _info;

    public string GetHardwareFingerprint() => _info.GenerateFingerprint();

    public double CalculateDriftScore(string existingFingerprint, DeviceHardwareInfo currentInfo)
    {
        return string.Equals(existingFingerprint, currentInfo.GenerateFingerprint(), StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
    }
}
