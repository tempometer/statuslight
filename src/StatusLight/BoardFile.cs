using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace StatusLight;

/// <summary>
/// Loads and validates a status file (YAML) into a <see cref="Board"/>.
/// All failures surface as <see cref="BoardFileException"/> with a human-readable reason.
/// </summary>
public static class BoardFile
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static Board Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException
                                    or IOException or UnauthorizedAccessException)
        {
            throw new BoardFileException(path, $"cannot read file: {ex.Message}");
        }

        Schema? schema;
        try
        {
            schema = Deserializer.Deserialize<Schema>(text);
        }
        catch (YamlException ex)
        {
            throw new BoardFileException(path, $"cannot parse YAML at line {ex.Start.Line}: {ex.Message}");
        }

        if (schema is null)
        {
            throw new BoardFileException(path, "file is empty or contains no YAML document");
        }

        var title = schema.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new BoardFileException(path, "'title' is required and must be a non-empty string");
        }

        var items = schema.Items ?? [];
        if (items.Count == 0)
        {
            throw new BoardFileException(path, "'items' must contain at least one entry");
        }

        var parsed = new List<BoardItem>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var name = item?.Name?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                throw new BoardFileException(path, $"items[{i}].name: required and must be a non-empty string");
            }

            if (!TryParseState(item!.State, out var state))
            {
                throw new BoardFileException(
                    path, $"items[{i}].state: must be one of green|yellow|red, got '{item.State ?? "null"}'");
            }

            var note = item.Note is { } n && !string.IsNullOrWhiteSpace(n) ? n.Trim() : null;
            parsed.Add(new BoardItem(name!, state, note));
        }

        return new Board(title!, parsed);
    }

    private static bool TryParseState(string? value, out BoardState state)
    {
        switch (value?.Trim().ToLowerInvariant())
        {
            case "green":
                state = BoardState.Green;
                return true;
            case "yellow":
                state = BoardState.Yellow;
                return true;
            case "red":
                state = BoardState.Red;
                return true;
            default:
                state = default;
                return false;
        }
    }

    private sealed class Schema
    {
        public string? Title { get; set; }

        public List<SchemaItem>? Items { get; set; }
    }

    private sealed class SchemaItem
    {
        public string? Name { get; set; }

        public string? State { get; set; }

        public string? Note { get; set; }
    }
}
