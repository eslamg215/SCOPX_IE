namespace SCOPX.Workstation.Devices;

public enum CaptureTransport { Usb, HdmiCaptureCard, NetworkStream, Simulator }
public enum CaptureState { Disconnected, Ready, Capturing, Faulted }

public sealed record CaptureDeviceDescriptor(
    string DeviceId,
    string DisplayName,
    CaptureTransport Transport,
    string? Manufacturer,
    string? Model,
    CaptureState State,
    string? CompatibilityNote = null);

public sealed record CaptureStartOptions(int Width, int Height, int FramesPerSecond, string PixelFormat = "MJPEG");
public sealed record CaptureStatus(CaptureState State, string? Message, DateTimeOffset UpdatedAt);

/// <summary>Provider boundary for hardware capture. Concrete drivers must be validated on target hardware.</summary>
public interface IDeviceCaptureAdapter : IAsyncDisposable
{
    CaptureDeviceDescriptor Descriptor { get; }
    Task<CaptureStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CaptureStartOptions options, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}


/// <summary>Explicit test-only device. It does not produce video and must never be presented as a real capture source.</summary>
public sealed class SimulatorCaptureAdapter : IDeviceCaptureAdapter
{
    private CaptureState _state = CaptureState.Ready;
    public CaptureDeviceDescriptor Descriptor => new("simulator://scopx-test-source", "SCOPX Test Source (No Video)", CaptureTransport.Simulator, "SCOPX", "Test Source", _state, "Test-only status simulator; no image/video is captured.");

    public Task<CaptureStatus> GetStatusAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new CaptureStatus(_state, "Simulation only — no real capture", DateTimeOffset.UtcNow));

    public Task StartAsync(CaptureStartOptions options, CancellationToken cancellationToken = default)
    {
        if (options.Width is < 1 or > 7680 || options.Height is < 1 or > 4320 || options.FramesPerSecond is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(options), "Requested capture settings are outside the safe test bounds.");
        if (_state == CaptureState.Capturing) throw new InvalidOperationException("Capture is already running.");
        _state = CaptureState.Capturing;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _state = CaptureState.Ready;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() { _state = CaptureState.Disconnected; return ValueTask.CompletedTask; }
}
