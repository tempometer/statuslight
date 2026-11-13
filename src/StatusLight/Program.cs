using System.Text;

namespace StatusLight;

public static class Program
{
    public static int Main(string[] args)
    {
        string? input = null;
        string? output = null;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--input" or "-i":
                case "--output" or "-o":
                    var isInput = arg is "--input" or "-i";
                    if (i + 1 >= args.Length || IsFlagLike(args[i + 1]))
                    {
                        Console.Error.WriteLine(
                            $"statuslight: {(isInput ? "--input" : "--output")} requires a value");
                        return 1;
                    }

                    if (isInput)
                    {
                        input = args[++i];
                    }
                    else
                    {
                        output = args[++i];
                    }

                    break;
                case "--help" or "-h":
                    Console.WriteLine(Usage);
                    return 0;
                default:
                    Console.Error.WriteLine($"statuslight: unknown argument '{arg}'");
                    Console.Error.WriteLine(Usage);
                    return 1;
            }
        }

        if (input is null || output is null)
        {
            Console.Error.WriteLine("statuslight: both --input and --output are required");
            Console.Error.WriteLine(Usage);
            return 1;
        }

        Board board;
        try
        {
            board = BoardFile.Load(input);
        }
        catch (BoardFileException ex)
        {
            Console.Error.WriteLine($"statuslight: {ex.SourcePath}: {ex.Message}");
            return 1;
        }

        try
        {
            if (IsSamePath(input, output))
            {
                Console.Error.WriteLine("statuslight: --input and --output must be different paths");
                return 1;
            }

            var html = Renderer.Render(board, DateTimeOffset.UtcNow);
            WriteAtomically(output, html);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"statuslight: {output}: cannot write file: {ex.Message}");
            return 1;
        }

        return 0;
    }

    private static bool IsFlagLike(string value) =>
        value.Length > 1 && value[0] == '-';

    private static bool IsSamePath(string left, string right) =>
        string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    /// <summary>
    /// Writes via a temp file in the target directory followed by a rename, so a
    /// failed write (disk full, permissions, invalid path) never truncates a
    /// previously good output file.
    /// </summary>
    private static void WriteAtomically(string output, string content)
    {
        var fullPath = Path.GetFullPath(output);
        var directory = Path.GetDirectoryName(fullPath) is { Length: > 0 } d ? d : ".";
        var temp = Path.Combine(directory, "." + Path.GetFileName(fullPath) + $".{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, content, new UTF8Encoding(false));
            File.Move(temp, fullPath, overwrite: true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            try
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
            catch
            {
                // best-effort cleanup; the original error is what matters
            }

            throw new IOException(ex.Message, ex);
        }
    }

    private const string Usage =
        """
        Usage: statuslight --input <status.yml> --output <board.html>

        Reads a YAML status file and writes a self-contained HTML traffic-light board.

          --input,  -i <path>   YAML status file (required)
          --output, -o <path>   HTML output file (required)
          --help,   -h          show this help
        """;
}
