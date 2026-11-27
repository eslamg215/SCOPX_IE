namespace SCOPX.Workstation.Devices;

/// <summary>Validates untrusted metadata supplied by capture adapters before it is displayed or used.</summary>
public static class CaptureDeviceDescriptorPolicy
{
    public static bool IsValid(CaptureDeviceDescriptor? descriptor, out string reason)
    {
        reason = "invalid_descriptor";
        if (descriptor is null) return false;
        if (!IsSafeText(descriptor.DeviceId, 1, 256)
            || !IsSafeText(descriptor.DisplayName, 1, 120)
            || !IsOptionalSafeText(descriptor.Manufacturer, 120)
            || !IsOptionalSafeText(descriptor.Model, 120)
            || !IsOptionalSafeText(descriptor.CompatibilityNote, 500))
            return false;

        if (!Enum.IsDefined(descriptor.Transport) || !Enum.IsDefined(descriptor.State))
            return false;

        var isSimulatorId = descriptor.DeviceId.StartsWith("simulator://", StringComparison.Ordinal);
        if (descriptor.Transport == CaptureTransport.Simulator)
        {
            if (!isSimulatorId
                || !descriptor.DisplayName.Contains("No Video", StringComparison.OrdinalIgnoreCase))
                return false;
        }
        else if (isSimulatorId)
        {
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static bool IsOptionalSafeText(string? value, int maxLength) =>
        value is null || IsSafeText(value, 0, maxLength);

    private static bool IsSafeText(string? value, int minLength, int maxLength) =>
        value is not null
        && value.Length >= minLength
        && value.Length <= maxLength
        && value == value.Trim()
        && !value.Any(char.IsControl);
}
