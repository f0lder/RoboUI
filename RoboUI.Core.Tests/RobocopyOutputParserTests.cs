using RoboUI.Core.Robocopy;

namespace RoboUI.Core.Tests;

public class RobocopyOutputParserTests
{
    [Theory]
    [InlineData("\t    New File  \t\t   123456\tC:\\full\\path.ext", "New File", 123456L, @"C:\full\path.ext")]
    [InlineData("\t    Newer     \t\t        42\tD:\\a b\\c d.txt", "Newer", 42L, @"D:\a b\c d.txt")]
    [InlineData("\t    Older     \t\t         0\tD:\\empty.txt", "Older", 0L, @"D:\empty.txt")]
    [InlineData("\t    Changed   \t\t 9876543210\t\\\\server\\share\\big.iso", "Changed", 9876543210L, @"\\server\share\big.iso")]
    [InlineData("\t  Nouveau fichier\t\t      10\tC:\\été\\fichier.txt", "Nouveau fichier", 10L, @"C:\été\fichier.txt")]
    [InlineData("\t\t\t      7\tC:\\noclass.txt", "", 7L, @"C:\noclass.txt")]
    public void Parses_file_lines_structurally(string line, string cls, long size, string path)
    {
        var evt = Assert.IsType<FileEvent>(new RobocopyOutputParser().ParseLine(line));
        Assert.Equal(cls, evt.FileClass);
        Assert.Equal(size, evt.Size);
        Assert.Equal(path, evt.Path);
        Assert.False(evt.IsExtra);
    }

    [Fact]
    public void Trailing_carriage_return_is_ignored()
    {
        var evt = Assert.IsType<FileEvent>(new RobocopyOutputParser().ParseLine("\t    New File  \t\t   5\tC:\\a.txt\r"));
        Assert.Equal(@"C:\a.txt", evt.Path);
    }

    [Theory]
    [InlineData("\t*EXTRA File \t\t      2048\tE:\\dst\\old.txt")]
    [InlineData("\t*EXTRA File \t\t      2048\t\\\\nas\\backup\\old.txt")]
    public void Extra_files_are_flagged(string line)
    {
        var evt = Assert.IsType<FileEvent>(new RobocopyOutputParser().ParseLine(line));
        Assert.True(evt.IsExtra);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("------------------------------------------------------------------------------")]
    [InlineData("               Total    Copied   Skipped  Mismatch    FAILED    Extras")]
    [InlineData("    Files :         3         3         0         0         0         0")]
    [InlineData("Waiting 5 seconds... Retrying...")]
    [InlineData("\t*EXTRA Dir        -1\tE:\\dst\\olddir\\")]
    [InlineData("\t  New Dir          3\tC:\\src\\sub\\")]
    [InlineData("\t    New File  \t\t   12\trelative\\path.txt")]
    [InlineData("100%")]
    public void Ignores_other_lines(string line)
    {
        Assert.Null(new RobocopyOutputParser().ParseLine(line));
    }

    [Fact]
    public void Error_header_is_combined_with_next_non_empty_line()
    {
        var parser = new RobocopyOutputParser();

        Assert.Null(parser.ParseLine("2025/11/26 10:00:00 ERROR 5 (0x00000005) Copying File C:\\src\\locked file.txt"));
        Assert.Null(parser.ParseLine(""));
        var evt = Assert.IsType<ErrorEvent>(parser.ParseLine("Access is denied."));

        Assert.Equal(5, evt.Code);
        Assert.Equal(@"C:\src\locked file.txt", evt.Path);
        Assert.Equal("Access is denied.", evt.Message);
        Assert.False(evt.IsRetryLimitExceeded);
        Assert.StartsWith("2025/11/26 10:00:00 ERROR 5", evt.Line);
    }

    [Fact]
    public void Localized_error_with_unc_path_is_detected()
    {
        var parser = new RobocopyOutputParser();
        Assert.Null(parser.ParseLine("2025/11/26 10:00:00 FEHLER 53 (0x00000035) Zugriff auf Zielverzeichnis \\\\nas\\share\\dir\\"));
        var evt = Assert.IsType<ErrorEvent>(parser.ParseLine("Der Netzwerkpfad wurde nicht gefunden."));
        Assert.Equal(53, evt.Code);
        Assert.Equal(@"\\nas\share\dir\", evt.Path);
    }

    [Fact]
    public void Error_without_path_has_null_path()
    {
        var parser = new RobocopyOutputParser();
        parser.ParseLine("2025/11/26 10:00:00 ERROR 112 (0x00000070) Something went wrong");
        var evt = Assert.IsType<ErrorEvent>(parser.ParseLine("There is not enough space on the disk."));
        Assert.Equal(112, evt.Code);
        Assert.Null(evt.Path);
    }

    [Fact]
    public void Retry_sequence_produces_each_error_and_retry_limit()
    {
        var parser = new RobocopyOutputParser();
        string[] lines =
        [
            "2025/11/26 10:00:00 ERROR 32 (0x00000020) Copying File C:\\src\\in use.db",
            "The process cannot access the file because it is being used by another process.",
            "Waiting 5 seconds... Retrying...",
            "2025/11/26 10:00:05 ERROR 32 (0x00000020) Copying File C:\\src\\in use.db",
            "The process cannot access the file because it is being used by another process.",
            "",
            "ERROR: RETRY LIMIT EXCEEDED.",
            "",
            "\t    New File  \t\t   100\tC:\\src\\next.txt",
        ];

        var events = lines.Select(parser.ParseLine).Where(e => e is not null).ToList();

        Assert.Collection(events,
            e => Assert.Equal(32, Assert.IsType<ErrorEvent>(e).Code),
            e => Assert.Equal(32, Assert.IsType<ErrorEvent>(e).Code),
            e => Assert.True(Assert.IsType<ErrorEvent>(e).IsRetryLimitExceeded),
            e => Assert.Equal(@"C:\src\next.txt", Assert.IsType<FileEvent>(e).Path));
    }

    [Fact]
    public void Flush_returns_error_still_waiting_for_message()
    {
        var parser = new RobocopyOutputParser();
        parser.ParseLine("ERROR 2 (0x00000002) Accessing Source Directory C:\\missing\\");
        var evt = parser.Flush();
        Assert.NotNull(evt);
        Assert.Equal(2, evt.Code);
        Assert.Null(evt.Message);
        Assert.Null(parser.Flush());
    }

    [Fact]
    public void Null_line_is_ignored()
    {
        Assert.Null(new RobocopyOutputParser().ParseLine(null));
    }
}
