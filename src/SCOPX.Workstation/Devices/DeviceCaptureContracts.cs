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
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new CaptureStatus(_state, "Simulation only — no real capture", DateTimeOffset.UtcNow));
    }

    public Task StartAsync(CaptureStartOptions options, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_state == CaptureState.Disconnected) throw new ObjectDisposedException(nameof(SimulatorCaptureAdapter));
        if (_state != CaptureState.Ready) throw new InvalidOperationException("The simulator is not ready to start.");
        if (!CaptureStartOptionsPolicy.IsValid(options))
            throw new ArgumentOutOfRangeException(nameof(options), "Requested capture settings are outside the supported test policy.");
        _state = CaptureState.Capturing;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_state == CaptureState.Disconnected) throw new ObjectDisposedException(nameof(SimulatorCaptureAdapter));
        if (_state == CaptureState.Capturing) _state = CaptureState.Ready;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() { _state = CaptureState.Disconnected; return ValueTask.CompletedTask; }
}
