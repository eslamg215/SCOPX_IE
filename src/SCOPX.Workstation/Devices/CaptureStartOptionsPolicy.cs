namespace SCOPX.Workstation.Devices;

/// <summary>Bounds capture requests before any adapter can allocate buffers or negotiate a stream.</summary>
public static class CaptureStartOptionsPolicy
{
    private static readonly HashSet<string> SupportedPixelFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "MJPEG", "YUY2", "NV12"
    };

    public static bool IsValid(CaptureStartOptions? options)
    {
        if (options is null) return false;
        if (options.Width is < 1 or > 7680
            || options.Height is < 1 or > 4320
            || options.FramesPerSecond is < 1 or > 120)
            return false;

        var format = options.PixelFormat;
        return !string.IsNullOrWhiteSpace(format)
            && format.Length <= 16
            && format == format.Trim()
            && !format.Any(char.IsControl)
            && SupportedPixelFormats.Contains(format);
    }
}
