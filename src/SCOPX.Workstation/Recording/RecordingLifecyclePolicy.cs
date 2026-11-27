namespace SCOPX.Workstation.Recording;

/// <summary>
/// Pure recording lifecycle transition policy. It does not open devices, create files,
/// or imply that a recording has been persisted or clinically finalized.
/// </summary>
public enum RecordingLifecycleState
{
    Idle,
    Preparing,
    Recording,
    Stopping,
    Finalized,
    Error
}

public static class RecordingLifecyclePolicy
{
    public static bool IsAllowed(RecordingLifecycleState current, RecordingLifecycleState next)
        => (current, next) switch
        {
            (RecordingLifecycleState.Idle, RecordingLifecycleState.Preparing) => true,
            (RecordingLifecycleState.Preparing, RecordingLifecycleState.Recording) => true,
            (RecordingLifecycleState.Preparing, RecordingLifecycleState.Idle) => true,
            (RecordingLifecycleState.Preparing, RecordingLifecycleState.Error) => true,
            (RecordingLifecycleState.Recording, RecordingLifecycleState.Stopping) => true,
            (RecordingLifecycleState.Recording, RecordingLifecycleState.Error) => true,
            (RecordingLifecycleState.Stopping, RecordingLifecycleState.Finalized) => true,
            (RecordingLifecycleState.Stopping, RecordingLifecycleState.Error) => true,
            (RecordingLifecycleState.Finalized, RecordingLifecycleState.Preparing) => true,
            (RecordingLifecycleState.Error, RecordingLifecycleState.Idle) => true,
            _ => false
        };

    public static RecordingLifecycleState Transition(RecordingLifecycleState current, RecordingLifecycleState next)
    {
        if (!IsAllowed(current, next))
            throw new InvalidOperationException($"Recording lifecycle transition {current} -> {next} is not allowed.");
        return next;
    }
}
