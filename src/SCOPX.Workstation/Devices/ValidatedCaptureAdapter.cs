namespace SCOPX.Workstation.Devices;

/// <summary>Rejects malformed status metadata returned by capture adapters.</summary>
public static class CaptureStatusPolicy
{
    public static bool IsValid(CaptureStatus? status)
    {
        if (status is null || !Enum.IsDefined(status.State) || status.UpdatedAt == default)
            return false;
        if (status.UpdatedAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return false;

        var message = status.Message;
        return message is null
            || (message.Length <= 500 && !message.Any(char.IsControl));
    }
}

/// <summary>
/// Validating decorator for capture adapters. It checks metadata and request bounds
/// before forwarding operations; it does not implement a hardware driver.
/// </summary>
public sealed class ValidatedCaptureAdapter : IDeviceCaptureAdapter
{
    private readonly IDeviceCaptureAdapter _inner;
    private readonly CaptureDeviceDescriptor _descriptor;
    private int _disposed;

    public ValidatedCaptureAdapter(IDeviceCaptureAdapter inner)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _descriptor = inner.Descriptor;
        if (!CaptureDeviceDescriptorPolicy.IsValid(_descriptor, out _))
            throw new ArgumentException("Capture adapter supplied an invalid device descriptor.", nameof(inner));
    }

    public CaptureDeviceDescriptor Descriptor => _descriptor;

    public async Task<CaptureStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var status = await _inner.GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (!CaptureStatusPolicy.IsValid(status))
            throw new InvalidOperationException("Capture adapter returned invalid status metadata.");
        return status;
    }

    public async Task StartAsync(CaptureStartOptions options, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (!CaptureStartOptionsPolicy.IsValid(options))
            throw new ArgumentOutOfRangeException(nameof(options), "Capture options failed validation.");

        var status = await GetStatusAsync(cancellationToken).ConfigureAwait(false);
        if (status.State != CaptureState.Ready)
            throw new InvalidOperationException("Capture adapter must be ready before starting.");

        await _inner.StartAsync(options, cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _inner.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            await _inner.DisposeAsync().ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(ValidatedCaptureAdapter));
    }
}
