using System.Runtime.InteropServices;

namespace RoboUI.Services;

public interface IKeepAwakeService
{
    /// <summary>Stops Windows from sleeping until <see cref="Release"/> is called. Call on the UI thread.</summary>
    void Acquire();

    /// <summary>Allows sleep again. Safe to call more than once. Call on the UI thread.</summary>
    void Release();
}

/// <summary>
/// Uses SetThreadExecutionState. The request belongs to the calling thread, so both calls must be
/// made on the same (UI) thread.
/// </summary>
public sealed class KeepAwakeService : IKeepAwakeService
{
    private const uint EsContinuous = 0x80000000;
    private const uint EsSystemRequired = 0x00000001;

    [DllImport("kernel32.dll")]
    private static extern uint SetThreadExecutionState(uint esFlags);

    public void Acquire() => SetThreadExecutionState(EsContinuous | EsSystemRequired);

    public void Release() => SetThreadExecutionState(EsContinuous);
}
