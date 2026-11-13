namespace StatusLight;

/// <summary>
/// Thrown when a status file cannot be read or does not conform to the schema.
/// Carries the offending path so the CLI can report it cleanly.
/// </summary>
public sealed class BoardFileException : Exception
{
    public string SourcePath { get; }

    public BoardFileException(string sourcePath, string message)
        : base(message)
    {
        SourcePath = sourcePath;
    }
}
