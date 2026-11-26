using RoboUI.Core.Robocopy;

namespace RoboUI.Core.Tests;

public class ExitCodeInterpreterTests
{
    [Theory]
    //          code  severity              copied extras mismatch failed
    [InlineData(0,  RunSeverity.Success, false, false, false, false)]
    [InlineData(1,  RunSeverity.Success, true,  false, false, false)]
    [InlineData(2,  RunSeverity.Success, false, true,  false, false)]
    [InlineData(3,  RunSeverity.Success, true,  true,  false, false)]
    [InlineData(4,  RunSeverity.Success, false, false, true,  false)]
    [InlineData(5,  RunSeverity.Success, true,  false, true,  false)]
    [InlineData(6,  RunSeverity.Success, false, true,  true,  false)]
    [InlineData(7,  RunSeverity.Success, true,  true,  true,  false)]
    [InlineData(8,  RunSeverity.Failure, false, false, false, true)]
    [InlineData(9,  RunSeverity.Failure, true,  false, false, true)]
    [InlineData(10, RunSeverity.Failure, false, true,  false, true)]
    [InlineData(11, RunSeverity.Failure, true,  true,  false, true)]
    [InlineData(12, RunSeverity.Failure, false, false, true,  true)]
    [InlineData(13, RunSeverity.Failure, true,  false, true,  true)]
    [InlineData(14, RunSeverity.Failure, false, true,  true,  true)]
    [InlineData(15, RunSeverity.Failure, true,  true,  true,  true)]
    [InlineData(16, RunSeverity.Fatal,   false, false, false, false)]
    [InlineData(24, RunSeverity.Fatal,   false, false, false, true)]
    public void Decodes_flags(int code, RunSeverity severity, bool copied, bool extras, bool mismatches, bool failed)
    {
        var r = ExitCodeInterpreter.Interpret(code);

        Assert.Equal(code, r.Code);
        Assert.Equal(severity, r.Severity);
        Assert.Equal(copied, r.FilesCopied);
        Assert.Equal(extras, r.ExtrasDetected);
        Assert.Equal(mismatches, r.MismatchesDetected);
        Assert.Equal(failed, r.SomeFilesFailed);
        Assert.Equal(severity == RunSeverity.Success, r.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(r.Message));
    }

    [Theory]
    [InlineData(0, "already up to date")]
    [InlineData(1, "copied successfully")]
    [InlineData(2, "extra files")]
    [InlineData(3, "copied successfully")]
    [InlineData(4, "do not match")]
    [InlineData(8, "could not be copied")]
    [InlineData(9, "other files were copied")]
    [InlineData(16, "Fatal")]
    public void Message_describes_result(int code, string expectedFragment)
    {
        Assert.Contains(expectedFragment, ExitCodeInterpreter.Interpret(code).Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Only_zero_means_nothing_to_do()
    {
        Assert.True(ExitCodeInterpreter.Interpret(0).NothingToDo);
        Assert.False(ExitCodeInterpreter.Interpret(2).NothingToDo);
    }

    [Fact]
    public void Negative_code_throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExitCodeInterpreter.Interpret(-1));
    }

    [Fact]
    public void Stopped_run_has_no_interpretation()
    {
        Assert.Null(new RunResult(-1, StoppedByUser: true, TimeSpan.Zero).Interpretation);
        Assert.Null(new RunResult(1, StoppedByUser: true, TimeSpan.Zero).Interpretation);
        Assert.NotNull(new RunResult(1, StoppedByUser: false, TimeSpan.Zero).Interpretation);
    }
}
