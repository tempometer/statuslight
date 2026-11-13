using StatusLight;

namespace StatusLight.Tests;

/// <summary>
/// End-to-end tests for the CLI entry point: argument parsing, exit codes and
/// the "never a stack trace" contract. xUnit runs test classes in parallel,
/// but only this class redirects the console, so the global state is safe.
/// </summary>
public sealed class ProgramTests : IDisposable
{
    private readonly StringWriter _stdout = new();
    private readonly StringWriter _stderr = new();
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "statuslight-cli-" + Guid.NewGuid().ToString("N"));

    public ProgramTests()
    {
        Console.SetOut(_stdout);
        Console.SetError(_stderr);
        Directory.CreateDirectory(_dir);
    }

    private string ValidYamlPath()
    {
        var path = Path.Combine(_dir, "status.yml");
        File.WriteAllText(path, "title: T\nitems:\n  - name: A\n    state: green\n");
        return path;
    }

    [Fact]
    public void Main_Help_Exits0_PrintsUsage()
    {
        var exit = Program.Main(["--help"]);

        Assert.Equal(0, exit);
        Assert.Contains("Usage:", _stdout.ToString());
        Assert.Contains("./-out.html", _stdout.ToString());
    }

    [Fact]
    public void Main_MissingOutputArg_Exits1()
    {
        var exit = Program.Main(["--input", ValidYamlPath()]);

        Assert.Equal(1, exit);
        Assert.Contains("--output", _stderr.ToString());
    }

    [Fact]
    public void Main_FlagWithoutValue_Exits1WithClearMessage()
    {
        var exit = Program.Main(["--input", ValidYamlPath(), "--output"]);

        Assert.Equal(1, exit);
        Assert.Contains("--output requires a value", _stderr.ToString());
    }

    [Fact]
    public void Main_FlagLikeValueRejected_Exits1()
    {
        var exit = Program.Main(["--input", ValidYamlPath(), "-o", "--help"]);

        Assert.Equal(1, exit);
        Assert.Contains("requires a value", _stderr.ToString());
    }

    [Fact]
    public void Main_UnknownArgument_Exits1()
    {
        var exit = Program.Main(["--bogus"]);

        Assert.Equal(1, exit);
        Assert.Contains("unknown argument", _stderr.ToString());
    }

    [Fact]
    public void Main_SameInputAndOutputPath_Exits1()
    {
        var path = ValidYamlPath();

        var exit = Program.Main(["-i", path, "-o", path]);

        Assert.Equal(1, exit);
        Assert.Contains("different paths", _stderr.ToString());
    }

    [Fact]
    public void Main_ValidInput_Exits0_WritesBoard()
    {
        var output = Path.Combine(_dir, "board.html");

        var exit = Program.Main(["-i", ValidYamlPath(), "-o", output]);

        Assert.Equal(0, exit);
        Assert.True(File.Exists(output));
        Assert.Contains("<h1>T</h1>", File.ReadAllText(output));
    }

    [Fact]
    public void Main_MissingInputFile_Exits1_NoStackTrace()
    {
        var missing = Path.Combine(_dir, "missing.yml");

        var exit = Program.Main(["-i", missing, "-o", Path.Combine(_dir, "board.html")]);

        Assert.Equal(1, exit);
        var stderr = _stderr.ToString();
        Assert.Contains("cannot read file", stderr);
        Assert.DoesNotContain("Exception", stderr);
    }

    [Fact]
    public void Main_EmptyOutputValue_Exits1_NoStackTrace()
    {
        var exit = Program.Main(["-i", ValidYamlPath(), "-o", ""]);

        Assert.Equal(1, exit);
        var stderr = _stderr.ToString();
        Assert.Contains("cannot write file", stderr);
        Assert.Contains("(empty)", stderr);
        Assert.DoesNotContain("Exception", stderr);
    }

    [Fact]
    public void Main_MissingOutputDirectory_Exits1_NamesTargetNotTemp()
    {
        var output = Path.Combine(_dir, "no-such-dir", "board.html");

        var exit = Program.Main(["-i", ValidYamlPath(), "-o", output]);

        Assert.Equal(1, exit);
        var stderr = _stderr.ToString();
        Assert.Contains("no-such-dir", stderr);
        Assert.DoesNotContain(".tmp", stderr);
        Assert.DoesNotContain("Exception", stderr);
    }

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
