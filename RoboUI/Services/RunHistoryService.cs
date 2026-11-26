using System.IO;
using RoboUI.Core.History;

namespace RoboUI.Services;

/// <summary>
/// App-side wrapper around <see cref="RunHistory"/>. <see cref="Added"/> is raised synchronously on
/// the caller's (UI) thread before saving, so the History page can update its list directly.
/// Saving problems are reported through <see cref="SaveFailed"/> instead of failing a run.
/// </summary>
public sealed class RunHistoryService(AppPaths paths)
{
    private readonly RunHistory _history = new(paths.HistoryFile);

    public event Action<RunRecord>? Added;
    public event Action<string>? SaveFailed;

    public Task<IReadOnlyList<RunRecord>> LoadAsync() => _history.LoadAsync();

    public async Task AddAsync(RunRecord run)
    {
        Added?.Invoke(run);
        try
        {
            await _history.AddAsync(run);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveFailed?.Invoke(ex.Message);
        }
    }

    public Task ClearAsync() => _history.ClearAsync();
}
