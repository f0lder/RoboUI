using RoboUI.Core.Models;
using RoboUI.Core.Robocopy;

namespace RoboUI.Core.Tests;

public class RobocopyCommandBuilderTests
{
    private static CopyJob Job(string source = @"C:\Src", string destination = @"E:\Dst") => new()
    {
        Source = source,
        Destination = destination,
    };

    [Theory]
    [InlineData(@"C:\Data", @"C:\Data")]
    [InlineData(@"C:\Data\", @"C:\Data")]
    [InlineData(@"C:\Data\\", @"C:\Data")]
    [InlineData(@"C:\My Data\", "\"C:\\My Data\"")]
    [InlineData(@"D:\", @"D:\")]
    [InlineData(@"D:", @"D:\")]
    [InlineData(@"d:\", @"D:\")]
    [InlineData(@"\\server\share\", @"\\server\share")]
    [InlineData(@"\\server\share\sub dir\", "\"\\\\server\\share\\sub dir\"")]
    [InlineData(@"  C:\Data  ", @"C:\Data")]
    public void QuotePath_handles_trailing_backslashes_roots_unc_and_spaces(string input, string expected)
    {
        Assert.Equal(expected, RobocopyCommandBuilder.QuotePath(input));
    }

    [Fact]
    public void Copy_pass_with_defaults()
    {
        var args = RobocopyCommandBuilder.Build(Job(), RobocopyPass.Copy);
        Assert.Equal(@"C:\Src E:\Dst /E /Z /MT:8 /R:2 /W:5 /BYTES /FP /NDL /NP /NJH", args);
    }

    [Fact]
    public void Scan_pass_adds_list_switches_and_no_log()
    {
        var args = RobocopyCommandBuilder.Build(Job(), RobocopyPass.Scan, @"C:\logs\a.log");
        Assert.Equal(@"C:\Src E:\Dst /E /Z /MT:8 /R:2 /W:5 /BYTES /FP /NDL /NP /NJH /L /NJS", args);
    }

    [Fact]
    public void Drive_roots_are_passed_unquoted_with_backslash()
    {
        var args = RobocopyCommandBuilder.Build(Job(@"D:\", @"F:"), RobocopyPass.Copy);
        Assert.StartsWith(@"D:\ F:\ /E", args);
    }

    [Fact]
    public void Unc_and_spaces_are_quoted_without_trailing_backslash()
    {
        var args = RobocopyCommandBuilder.Build(Job(@"\\nas\photos 2024\", @"E:\Backup Folder\"), RobocopyPass.Copy);
        Assert.StartsWith("\"\\\\nas\\photos 2024\" \"E:\\Backup Folder\" ", args);
        Assert.DoesNotContain("\\\"", args);
    }

    [Fact]
    public void Options_map_to_switches()
    {
        var job = Job();
        job.IncludeSubfolders = false;
        job.Restartable = false;
        job.SkipOlder = true;
        job.Threads = 32;
        job.Retries = 0;
        job.WaitSeconds = 1;

        var args = RobocopyCommandBuilder.Build(job, RobocopyPass.Copy);

        Assert.Equal(@"C:\Src E:\Dst /XO /MT:32 /R:0 /W:1 /BYTES /FP /NDL /NP /NJH", args);
    }

    [Fact]
    public void Excludes_are_appended_and_quoted_when_needed()
    {
        var job = Job();
        job.ExcludeFiles = ["*.tmp", "Thumbs.db", "my file.txt", "  ", ""];
        job.ExcludeDirs = ["node_modules", @"C:\Src\Cache Dir\", " "];

        var args = RobocopyCommandBuilder.Build(job, RobocopyPass.Copy);

        Assert.Contains("/XF *.tmp Thumbs.db \"my file.txt\" /XD node_modules \"C:\\Src\\Cache Dir\" /BYTES", args);
    }

    [Fact]
    public void Empty_exclude_lists_add_nothing()
    {
        var job = Job();
        job.ExcludeFiles = ["", " "];
        var args = RobocopyCommandBuilder.Build(job, RobocopyPass.Copy);
        Assert.DoesNotContain("/XF", args);
        Assert.DoesNotContain("/XD", args);
    }

    [Fact]
    public void Mirror_overrides_subfolders_on_copy_pass()
    {
        var job = Job();
        job.Mode = CopyMode.Mirror;
        job.IncludeSubfolders = true;

        var args = RobocopyCommandBuilder.Build(job, RobocopyPass.Copy);

        Assert.Contains(" /MIR ", args);
        Assert.DoesNotContain(" /E ", args);
    }

    [Fact]
    public void Mirror_is_never_used_on_scan_pass()
    {
        var job = Job();
        job.Mode = CopyMode.Mirror;
        job.IncludeSubfolders = false;

        var args = RobocopyCommandBuilder.Build(job, RobocopyPass.Scan);

        Assert.DoesNotContain("/MIR", args);
        Assert.DoesNotContain("/PURGE", args);
        Assert.Contains(" /E ", args);
    }

    [Fact]
    public void Test_run_is_the_copy_command_plus_list_only()
    {
        var copy = RobocopyCommandBuilder.Build(Job(), RobocopyPass.Copy);
        var test = RobocopyCommandBuilder.Build(Job(), RobocopyPass.TestRun);
        Assert.Equal(copy + " /L", test);
    }

    [Fact]
    public void Test_run_keeps_mirror_and_move_but_is_list_only_and_logged()
    {
        var job = Job();
        job.Mode = CopyMode.Mirror;
        var mirror = RobocopyCommandBuilder.Build(job, RobocopyPass.TestRun, @"C:\logs\t.log");
        Assert.Contains(" /MIR ", mirror);
        Assert.Contains(" /L ", mirror);
        Assert.EndsWith(@"/UNILOG+:C:\logs\t.log /TEE", mirror);

        job.Mode = CopyMode.Move;
        var move = RobocopyCommandBuilder.Build(job, RobocopyPass.TestRun);
        Assert.Contains(" /MOVE ", move);
        Assert.EndsWith(" /L", move);
    }

    [Fact]
    public void Move_adds_move_and_include_switches_on_copy_pass()
    {
        var job = Job();
        job.Mode = CopyMode.Move;
        var args = RobocopyCommandBuilder.Build(job, RobocopyPass.Copy);
        Assert.Equal(@"C:\Src E:\Dst /E /MOVE /IS /IT /IM /Z /MT:8 /R:2 /W:5 /BYTES /FP /NDL /NP /NJH", args);
    }

    [Fact]
    public void Scan_pass_never_moves_but_counts_the_same_files()
    {
        var job = Job();
        job.Mode = CopyMode.Move;
        var args = RobocopyCommandBuilder.Build(job, RobocopyPass.Scan);
        Assert.DoesNotContain("/MOVE", args);
        Assert.Contains(" /IS /IT /IM ", args);
        Assert.EndsWith("/L /NJS", args);
    }

    [Fact]
    public void Move_is_absent_unless_enabled()
    {
        Assert.DoesNotContain("/MOVE", RobocopyCommandBuilder.Build(Job(), RobocopyPass.Copy));
        Assert.DoesNotContain("/IS", RobocopyCommandBuilder.Build(Job(), RobocopyPass.Copy));
    }

    [Fact]
    public void No_mirror_or_purge_unless_enabled()
    {
        var args = RobocopyCommandBuilder.Build(Job(), RobocopyPass.Copy, @"C:\logs\x.log");
        Assert.DoesNotContain("/MIR", args);
        Assert.DoesNotContain("/PURGE", args);
    }

    [Fact]
    public void Log_is_added_on_copy_pass_when_enabled()
    {
        var args = RobocopyCommandBuilder.Build(Job(), RobocopyPass.Copy, @"C:\Users\Me\AppData\Roaming\RoboUI\logs\job_1.log");
        Assert.EndsWith(@"/NJH /UNILOG+:C:\Users\Me\AppData\Roaming\RoboUI\logs\job_1.log /TEE", args);
    }

    [Fact]
    public void Log_path_with_spaces_is_quoted()
    {
        var args = RobocopyCommandBuilder.Build(Job(), RobocopyPass.Copy, @"C:\Users\John Doe\logs\a.log");
        Assert.EndsWith("/UNILOG+:\"C:\\Users\\John Doe\\logs\\a.log\" /TEE", args);
    }

    [Fact]
    public void Log_is_omitted_when_disabled_or_missing()
    {
        var job = Job();
        job.WriteLog = false;
        Assert.DoesNotContain("/UNILOG", RobocopyCommandBuilder.Build(job, RobocopyPass.Copy, @"C:\l.log"));
        Assert.DoesNotContain("/UNILOG", RobocopyCommandBuilder.Build(Job(), RobocopyPass.Copy));
    }
}
