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
            switch (args[i])
            {
                case "--input" or "-i" when i + 1 < args.Length:
                    input = args[++i];
                    break;
                case "--output" or "-o" when i + 1 < args.Length:
                    output = args[++i];
                    break;
                case "--help" or "-h":
                    Console.WriteLine(Usage);
                    return 0;
                default:
                    Console.Error.WriteLine($"statuslight: unknown argument '{args[i]}'");
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

        var html = Renderer.Render(board, DateTimeOffset.UtcNow);
        try
        {
            File.WriteAllText(output, html, new UTF8Encoding(false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                    or DirectoryNotFoundException or System.Security.SecurityException)
        {
            Console.Error.WriteLine($"statuslight: {output}: cannot write file: {ex.Message}");
            return 1;
        }

        return 0;
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
