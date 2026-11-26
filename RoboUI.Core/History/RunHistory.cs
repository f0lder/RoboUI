using System.Text.Json;
using System.Text.Json.Serialization;

namespace RoboUI.Core.History;

/// <summary>
/// The list of past runs, newest first, kept in a JSON file. Only the newest
/// <see cref="MaxEntries"/> runs are kept. Writes are atomic (temp file, then replace), and a
/// corrupt file is set aside as "*.bad" instead of failing. All members are safe to call
/// concurrently; file access happens off the caller's thread.
/// </summary>
public sealed class RunHistory(string filePath, int maxEntries = RunHistory.DefaultMaxEntries)
{
    public const int DefaultMaxEntries = 200;
    private const int FileVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private List<RunRecord> _runs = [];
    private bool _loaded;

    public string FilePath { get; } = filePath;
    public int MaxEntries { get; } = maxEntries;

    /// <summary>Reads the file (once) and returns the runs, newest first.</summary>
    public async Task<IReadOnlyList<RunRecord>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            return _runs.ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Adds a run at the top, drops the oldest beyond the limit, and saves.</summary>
    public async Task AddAsync(RunRecord run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
            _runs.Insert(0, run);
            if (_runs.Count > MaxEntries) _runs.RemoveRange(MaxEntries, _runs.Count - MaxEntries);
            await WriteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Removes all runs. Log files are not touched.</summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _runs = [];
            _loaded = true;
            await WriteAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_loaded) return;
        _runs = await ReadAsync(cancellationToken).ConfigureAwait(false);
        _loaded = true;
    }

    private async Task<List<RunRecord>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath)) return [];
        try
        {
            await using var stream = File.OpenRead(FilePath);
            var file = await JsonSerializer.DeserializeAsync<HistoryFile>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            var runs = file?.Runs?.Where(r => r is not null).ToList() ?? [];
            return runs.Count > MaxEntries ? runs.GetRange(0, MaxEntries) : runs;
        }
        catch (JsonException)
        {
            // Keep the damaged file for inspection and start a fresh history.
            File.Move(FilePath, FilePath + ".bad", overwrite: true);
            return [];
        }
    }

    private async Task WriteAsync(CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        var temp = FilePath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            var file = new HistoryFile { Version = FileVersion, Runs = _runs };
            await JsonSerializer.SerializeAsync(stream, file, JsonOptions, cancellationToken).ConfigureAwait(false);
        }
        File.Move(temp, FilePath, overwrite: true);
    }

    private sealed class HistoryFile
    {
        public int Version { get; set; }
        public List<RunRecord>? Runs { get; set; }
    }
}
