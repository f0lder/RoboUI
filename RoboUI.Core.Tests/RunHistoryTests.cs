using RoboUI.Core.History;
using RoboUI.Core.Models;

namespace RoboUI.Core.Tests;

public sealed class RunHistoryTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "RoboUI.HistoryTests." + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_dir, "history.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private static RunRecord Run(string source, RunOutcome outcome = RunOutcome.Completed) => new()
    {
        StartedAt = DateTimeOffset.Now,
        Duration = TimeSpan.FromSeconds(42),
        Outcome = outcome,
        ExitCode = 1,
        FilesDone = 10,
        BytesDone = 1000,
        LogPath = @"C:\logs\a.log",
        Job = new CopyJob { Source = source, Destination = @"E:\Dst", Mode = CopyMode.Move, Threads = 16 },
    };

    [Fact]
    public async Task Missing_file_gives_empty_history()
    {
        Assert.Empty(await new RunHistory(FilePath).LoadAsync());
    }

    [Fact]
    public async Task Added_runs_persist_newest_first()
    {
        var history = new RunHistory(FilePath);
        await history.AddAsync(Run(@"C:\first"));
        await history.AddAsync(Run(@"C:\second", RunOutcome.Stopped));

        var reloaded = await new RunHistory(FilePath).LoadAsync();

        Assert.Equal([@"C:\second", @"C:\first"], reloaded.Select(r => r.Job.Source));
        Assert.Equal(RunOutcome.Stopped, reloaded[0].Outcome);
    }

    [Fact]
    public async Task Job_settings_round_trip()
    {
        var original = Run(@"C:\src");
        original.Job.ExcludeFiles = ["*.tmp", "my file.txt"];
        await new RunHistory(FilePath).AddAsync(original);

        var r = (await new RunHistory(FilePath).LoadAsync()).Single();

        Assert.Equal(original.Id, r.Id);
        Assert.Equal(TimeSpan.FromSeconds(42), r.Duration);
        Assert.Equal(CopyMode.Move, r.Job.Mode);
        Assert.Equal(16, r.Job.Threads);
        Assert.Equal(["*.tmp", "my file.txt"], r.Job.ExcludeFiles);
        Assert.Contains("\"Move\"", await File.ReadAllTextAsync(FilePath)); // enums stored as text
    }

    [Fact]
    public async Task Only_the_newest_entries_are_kept()
    {
        var history = new RunHistory(FilePath, maxEntries: 3);
        for (var i = 1; i <= 5; i++) await history.AddAsync(Run($@"C:\run{i}"));

        var reloaded = await new RunHistory(FilePath, maxEntries: 3).LoadAsync();

        Assert.Equal([@"C:\run5", @"C:\run4", @"C:\run3"], reloaded.Select(r => r.Job.Source));
    }

    [Fact]
    public async Task Clear_removes_everything()
    {
        var history = new RunHistory(FilePath);
        await history.AddAsync(Run(@"C:\a"));
        await history.ClearAsync();

        Assert.Empty(await new RunHistory(FilePath).LoadAsync());
    }

    [Fact]
    public async Task Corrupt_file_is_set_aside_and_history_starts_fresh()
    {
        Directory.CreateDirectory(_dir);
        await File.WriteAllTextAsync(FilePath, "{ not json");

        var history = new RunHistory(FilePath);
        Assert.Empty(await history.LoadAsync());
        Assert.True(File.Exists(FilePath + ".bad"));

        await history.AddAsync(Run(@"C:\after"));
        Assert.Single(await new RunHistory(FilePath).LoadAsync());
    }

    [Fact]
    public async Task Concurrent_adds_are_all_kept()
    {
        var history = new RunHistory(FilePath);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(i => history.AddAsync(Run($@"C:\p{i}"))));
        Assert.Equal(20, (await new RunHistory(FilePath).LoadAsync()).Count);
    }
}
