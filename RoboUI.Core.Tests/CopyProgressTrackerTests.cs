using RoboUI.Core.Progress;
using RoboUI.Core.Robocopy;

namespace RoboUI.Core.Tests;

public class CopyProgressTrackerTests
{
    private static FileEvent File(string path, long size) => new("New File", size, path);
    private static FileEvent Extra(string path, long size) => new("*EXTRA File", size, path);
    private static ErrorEvent Error(string path) => new($"ERROR 32 (0x00000020) Copying File {path}", 32, path, "In use.");
    private static readonly ErrorEvent RetryLimit = new("ERROR: RETRY LIMIT EXCEEDED.", null, null, null, IsRetryLimitExceeded: true);

    private static CopyProgressTracker Copying(long? totalFiles = null, long? totalBytes = null)
    {
        var t = new CopyProgressTracker();
        if (totalFiles is not null) t.SetTotals(totalFiles.Value, totalBytes ?? 0);
        t.SetPhase(CopyPhase.Copying);
        return t;
    }

    [Fact]
    public void File_is_credited_only_when_next_file_starts()
    {
        var t = Copying();
        t.Report(File(@"C:\a", 100));

        var s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(0, s.FilesDone);
        Assert.Equal(0, s.BytesDone);
        Assert.Equal(@"C:\a", s.CurrentFile);

        t.Report(File(@"C:\b", 50));
        s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(1, s.FilesDone);
        Assert.Equal(100, s.BytesDone);
        Assert.Equal(@"C:\b", s.CurrentFile);
    }

    [Fact]
    public void Last_file_is_credited_when_pass_ends_normally()
    {
        var t = Copying();
        t.Report(File(@"C:\a", 100));
        t.Report(File(@"C:\b", 50));
        t.CompletePass(stoppedByUser: false);

        var s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(2, s.FilesDone);
        Assert.Equal(150, s.BytesDone);
        Assert.Equal(CopyPhase.Finished, s.Phase);
    }

    [Fact]
    public void Last_file_is_not_credited_when_stopped()
    {
        var t = Copying();
        t.Report(File(@"C:\a", 100));
        t.Report(File(@"C:\b", 50));
        t.CompletePass(stoppedByUser: true);

        var s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(1, s.FilesDone);
        Assert.Equal(100, s.BytesDone);
    }

    [Fact]
    public void Extras_do_not_count()
    {
        var t = Copying();
        t.Report(Extra(@"E:\old", 999));
        t.Report(File(@"C:\a", 10));
        t.Report(Extra(@"E:\old2", 999));
        t.CompletePass(false);

        var s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(1, s.FilesDone);
        Assert.Equal(10, s.BytesDone);
    }

    [Fact]
    public void Extras_are_counted_separately_outside_scan()
    {
        var t = Copying();
        t.Report(Extra(@"E:\old1", 100));
        t.Report(Extra(@"E:\old2", 50));
        var s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(2, s.ExtraFiles);
        Assert.Equal(150, s.ExtraBytes);

        var scan = new CopyProgressTracker();
        scan.SetPhase(CopyPhase.Scanning);
        scan.Report(Extra(@"E:\old1", 100));
        Assert.Equal(0, scan.GetSnapshot(TimeSpan.Zero).ExtraFiles);
    }

    [Fact]
    public void Repeated_file_line_on_retry_is_counted_once()
    {
        var t = Copying();
        t.Report(File(@"C:\a", 10));
        t.Report(Error(@"C:\a"));
        t.Report(File(@"C:\A", 10));
        t.CompletePass(false);

        Assert.Equal(1, t.GetSnapshot(TimeSpan.Zero).FilesDone);
    }

    [Fact]
    public void File_that_hit_retry_limit_is_failed_not_credited()
    {
        var t = Copying();
        t.Report(File(@"C:\locked", 1000));
        t.Report(Error(@"C:\locked"));
        t.Report(Error(@"C:\locked"));
        t.Report(RetryLimit);
        t.Report(File(@"C:\next", 5));
        t.CompletePass(false);

        var s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(1, s.FailedFiles);
        Assert.Equal(1, s.FilesDone);
        Assert.Equal(5, s.BytesDone);
    }

    [Fact]
    public void Retry_limit_for_a_folder_does_not_fail_the_previous_file()
    {
        var t = Copying();
        t.Report(File(@"C:\ok", 1000));
        t.Report(Error(@"C:\denied\"));
        t.Report(RetryLimit);
        t.CompletePass(false);

        var s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(0, s.FailedFiles);
        Assert.Equal(1000, s.BytesDone);
    }

    [Fact]
    public void Scan_phase_counts_scanned_files_and_ignores_errors()
    {
        var t = new CopyProgressTracker();
        t.SetPhase(CopyPhase.Scanning);
        t.Report(File(@"C:\a", 10));
        t.Report(File(@"C:\b", 20));
        t.Report(Extra(@"E:\x", 5));
        t.Report(Error(@"C:\c"));

        var s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(2, s.ScannedFiles);
        Assert.Equal(30, s.ScannedBytes);
        Assert.Equal(0, s.FilesDone);
        Assert.Empty(t.DrainErrors(500, out _));
    }

    [Fact]
    public void Errors_are_drained_keeping_the_newest()
    {
        var t = Copying();
        for (var i = 0; i < 10; i++) t.Report(Error($@"C:\f{i}"));

        var drained = t.DrainErrors(3, out var dropped);
        Assert.Equal(7, dropped);
        Assert.Equal([@"C:\f7", @"C:\f8", @"C:\f9"], drained.Select(e => e.Path));
        Assert.Empty(t.DrainErrors(3, out _));
    }

    [Fact]
    public void Percent_uses_bytes_then_files_and_is_null_without_totals()
    {
        var t = Copying(totalFiles: 4, totalBytes: 1000);
        t.Report(File(@"C:\a", 250));
        t.Report(File(@"C:\b", 250));
        Assert.Equal(25, t.GetSnapshot(TimeSpan.Zero).Percent);

        var zeroBytes = Copying(totalFiles: 2, totalBytes: 0);
        zeroBytes.Report(File(@"C:\a", 0));
        zeroBytes.Report(File(@"C:\b", 0));
        Assert.Equal(50, zeroBytes.GetSnapshot(TimeSpan.Zero).Percent);

        var nothing = Copying(totalFiles: 0, totalBytes: 0);
        Assert.Equal(100, nothing.GetSnapshot(TimeSpan.Zero).Percent);

        Assert.Null(Copying().GetSnapshot(TimeSpan.Zero).Percent);
    }

    [Fact]
    public void Percent_never_exceeds_100()
    {
        var t = Copying(totalFiles: 1, totalBytes: 10);
        t.Report(File(@"C:\a", 50));
        t.CompletePass(false);
        Assert.Equal(100, t.GetSnapshot(TimeSpan.Zero).Percent);
    }

    [Fact]
    public void Speed_and_eta_come_from_snapshots_during_copy()
    {
        var t = Copying(totalFiles: 3, totalBytes: 3000);
        t.GetSnapshot(TimeSpan.FromSeconds(0));
        t.Report(File(@"C:\a", 1000));
        t.Report(File(@"C:\b", 1000)); // credits a: 1000 bytes

        var s = t.GetSnapshot(TimeSpan.FromSeconds(10));
        Assert.Equal(100, s.BytesPerSecond);
        Assert.Equal(TimeSpan.FromSeconds(20), s.Eta);
    }

    [Fact]
    public void Concurrent_reports_are_counted_exactly()
    {
        var t = Copying();
        Parallel.For(0, 10_000, i => t.Report(File($@"C:\f{i}", 1)));
        t.CompletePass(false);
        var s = t.GetSnapshot(TimeSpan.Zero);
        Assert.Equal(10_000, s.FilesDone);
        Assert.Equal(10_000, s.BytesDone);
    }
}
