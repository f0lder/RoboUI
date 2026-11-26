using RoboUI.Core.Models;
using RoboUI.Core.Validation;

namespace RoboUI.Core.Tests;

public sealed class JobValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RoboUI.Tests." + Guid.NewGuid().ToString("N"));
    private readonly string _source;

    public JobValidatorTests()
    {
        _source = Path.Combine(_root, "Source Folder");
        Directory.CreateDirectory(_source);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private CopyJob Job(string? destination = null) => new()
    {
        Source = _source,
        Destination = destination ?? Path.Combine(_root, "Destination"),
    };

    [Fact]
    public void Valid_job_has_no_errors()
    {
        Assert.Empty(JobValidator.Validate(Job()));
    }

    [Fact]
    public void Missing_source_is_reported()
    {
        var job = Job();
        job.Source = Path.Combine(_root, "does-not-exist");
        Assert.Contains(JobValidator.Validate(job), e => e.Contains("does not exist"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_paths_are_reported(string path)
    {
        var job = new CopyJob { Source = path, Destination = path };
        var errors = JobValidator.Validate(job);
        Assert.Contains("Choose a source folder.", errors);
        Assert.Contains("Choose a destination folder.", errors);
    }

    [Fact]
    public void Relative_paths_are_rejected()
    {
        var job = new CopyJob { Source = "relative", Destination = @"other\dir" };
        var errors = JobValidator.Validate(job);
        Assert.Contains(errors, e => e.StartsWith("The source must be a full path"));
        Assert.Contains(errors, e => e.StartsWith("The destination must be a full path"));
    }

    [Fact]
    public void Destination_equal_to_source_is_rejected()
    {
        Assert.Contains(JobValidator.Validate(Job(_source + "\\")), e => e.Contains("inside"));
        Assert.Contains(JobValidator.Validate(Job(_source.ToUpperInvariant())), e => e.Contains("inside"));
    }

    [Fact]
    public void Destination_inside_source_is_rejected()
    {
        Assert.Contains(JobValidator.Validate(Job(Path.Combine(_source, "backup"))), e => e.Contains("inside"));
    }

    [Fact]
    public void Sibling_with_common_prefix_is_allowed()
    {
        // "Source Folder 2" starts with "Source Folder" as text but is not inside it.
        Assert.Empty(JobValidator.Validate(Job(_source + " 2")));
    }

    [Fact]
    public void Destination_containing_source_is_allowed()
    {
        Assert.Empty(JobValidator.Validate(Job(_root)));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(128, true)]
    [InlineData(129, false)]
    public void Threads_must_be_1_to_128(int threads, bool valid)
    {
        var job = Job();
        job.Threads = threads;
        Assert.Equal(valid, JobValidator.Validate(job).Count == 0);
    }

    [Fact]
    public void Negative_retries_and_wait_are_rejected()
    {
        var job = Job();
        job.Retries = -1;
        job.WaitSeconds = -1;
        Assert.Equal(2, JobValidator.Validate(job).Count);
    }

    [Theory]
    [InlineData(@"C:\Data", @"C:\Data\Sub", true)]
    [InlineData(@"C:\Data", @"c:\data", true)]
    [InlineData(@"C:\Data", @"C:\Data2", false)]
    [InlineData(@"C:\", @"C:\Anything", true)]
    [InlineData(@"\\nas\share", @"\\NAS\share\x", true)]
    [InlineData(@"\\nas\share", @"\\nas\share2", false)]
    public void IsSameOrInside_compares_by_folder_boundaries(string parent, string path, bool expected)
    {
        Assert.Equal(expected, JobValidator.IsSameOrInside(path, parent));
    }
}
