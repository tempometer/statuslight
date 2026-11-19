using System.Globalization;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;
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
        .Build();

    private const string UpdatedFormat = "yyyy-MM-dd HH:mm";
    private const string ClosedDateFormat = "yyyy-MM-dd";

    public static Board Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw new BoardFileException(path, $"cannot read file: {ex.Message}");
        }

        var documentCount = CountDocuments(path, text);
        if (documentCount == 0)
        {
            throw new BoardFileException(path, "file is empty or contains no YAML document");
        }

        if (documentCount > 1)
        {
            throw new BoardFileException(
                path, $"expected a single YAML document, found {documentCount}");
        }

        Schema? schema;
        try
        {
            schema = Deserializer.Deserialize<Schema>(text);
        }
        catch (YamlException ex)
        {
            throw new BoardFileException(path, $"invalid content at line {ex.Start.Line}: {ex.Message}");
        }

        if (schema is null)
        {
            throw new BoardFileException(path, "file contains no YAML document");
        }

        var vocabulary = ParseStates(path, schema.States);
        var isV2 = schema.Board is not null || schema.Sections is not null ||
                   schema.Queue is not null || schema.Closed is not null;
        return isV2
            ? LoadSectionsBoard(path, schema, vocabulary)
            : LoadLegacyBoard(path, schema, vocabulary);
    }

    private static IReadOnlyDictionary<string, StateInfo> ParseStates(
        string path, Dictionary<string, StateSchema>? states)
    {
        if (states is null || states.Count == 0)
        {
            return States.Defaults;
        }

        var vocabulary = new Dictionary<string, StateInfo>(StringComparer.Ordinal);
        var order = 0;
        foreach (var (rawKey, s) in states)
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                throw new BoardFileException(path, "states: keys must be non-empty");
            }

            var key = rawKey.Trim();
            var stock = States.Stock(key);
            var label = string.IsNullOrWhiteSpace(s?.Label) ? key : s.Label!.Trim();
            var rank = s?.Rank ?? stock?.Rank;
            if (rank is null)
            {
                throw new BoardFileException(
                    path, $"states.{key}.rank: required for non-stock states " +
                    "(stock states inherit their usual rank)");
            }

            var color = string.IsNullOrWhiteSpace(s?.Color) ? stock?.Color ?? States.FallbackColor : s!.Color!.Trim();
            if (color.Length != 7 || color[0] != '#' ||
                color.Skip(1).Any(c => !"0123456789abcdefABCDEF".Contains(c)))
            {
                throw new BoardFileException(
                    path, $"states.{key}.color: must be '#RRGGBB', got '{color}'");
            }

            if (vocabulary.ContainsKey(key) ||
                vocabulary.Keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
            {
                throw new BoardFileException(
                    path, $"states: duplicate key '{key}' (keys are case-insensitive)");
            }

            vocabulary[key] = new StateInfo(key, label, rank.Value, color.ToLowerInvariant(), order++);
        }

        return vocabulary;
    }

    private static StateInfo Resolve(
        string path, IReadOnlyDictionary<string, StateInfo> vocabulary, string? key, string where)
    {
        if (key is null)
        {
            throw new BoardFileException(path, $"{where}: required and must be a non-empty string");
        }

        var trimmed = key.Trim();
        if (!vocabulary.TryGetValue(trimmed, out var state))
        {
            // v0.1 accepted GREEN/Green; keep case-insensitive matching for custom vocabularies too
            state = vocabulary.FirstOrDefault(kv => string.Equals(kv.Key, trimmed, StringComparison.OrdinalIgnoreCase)).Value;
        }

        if (state is null)
        {
            var known = string.Join(", ", vocabulary.Keys.OrderBy(k => k, StringComparer.Ordinal));
            throw new BoardFileException(path, $"{where}: unknown state '{key}' (known: {known})");
        }

        return state;
    }

    private static int StockRank(string key) => key.Trim() switch
    {
        "green" => States.Green.Rank,
        "yellow" => States.Yellow.Rank,
        "red" => States.Red.Rank,
        "gray" => States.Gray.Rank,
        "closed" => States.Closed.Rank,
        _ => 0
    };

    private static string StockColor(string key) => key.Trim() switch
    {
        "green" => States.Green.Color,
        "yellow" => States.Yellow.Color,
        "red" => States.Red.Color,
        "gray" => States.Gray.Color,
        "closed" => States.Closed.Color,
        _ => States.FallbackColor
    };

    private static Board LoadSectionsBoard(
        string path, Schema schema, IReadOnlyDictionary<string, StateInfo> vocabulary)
    {
        if (schema.Items is not null)
        {
            throw new BoardFileException(
                path, "top-level 'items' belongs to the legacy flat schema and cannot " +
                "be combined with v2 keys ('board', 'sections', 'queue', 'closed') — " +
                "use one schema or the other, not both");
        }

        if (schema.Title is not null)
        {
            throw new BoardFileException(
                path, "top-level 'title' is legacy; the v2 board schema uses 'board.title'");
        }

        var board = schema.Board;
        var title = board?.Title?.Trim();
        if (string.IsNullOrEmpty(title))
        {
            throw new BoardFileException(
                path, "'board.title' is required and must be a non-empty string");
        }

        var sections = schema.Sections ?? [];
        if (sections.Count == 0)
        {
            throw new BoardFileException(path, "'sections' must contain at least one entry");
        }

        var defaultTtl = ParseTtl(path, board?.StaleAfterHours, "board.stale_after_hours", 48);
        var theme = ParseTheme(path, board?.Theme);

        var parsed = new List<Section>(sections.Count);
        for (var i = 0; i < sections.Count; i++)
        {
            parsed.Add(ParseSection(path, sections[i], vocabulary, defaultTtl, $"sections[{i}]"));
        }

        return new Board
        {
            Title = title!,
            Sections = parsed,
            Description = TrimOrNull(board?.Description),
            Footer = TrimOrNull(board?.Footer),
            StaleAfterHours = defaultTtl,
            Theme = theme,
            Queue = ParseQueue(path, schema.Queue),
            Closed = ParseClosed(path, schema.Closed)
        };
    }

    private static Section ParseSection(
        string path,
        SectionSchema s,
        IReadOnlyDictionary<string, StateInfo> vocabulary,
        int defaultTtl,
        string where)
    {
        var name = s?.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new BoardFileException(path, $"{where}.name: required and must be a non-empty string");
        }

        var updated = s?.Updated?.Trim();
        if (string.IsNullOrEmpty(updated))
        {
            throw new BoardFileException(
                path, $"{where}.updated: required, 'YYYY-MM-DD HH:MM' (date AND time)");
        }

        if (!DateTime.TryParseExact(updated, UpdatedFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var updatedUtc))
        {
            throw new BoardFileException(
                path, $"{where}.updated: must be 'YYYY-MM-DD HH:MM' (date AND time), got '{updated}'");
        }

        var ttl = ParseTtl(path, s?.StaleAfterHours, $"{where}.stale_after_hours", defaultTtl);

        var rawItems = s?.Items ?? [];
        var items = new List<BoardItem>(rawItems.Count);
        for (var j = 0; j < rawItems.Count; j++)
        {
            items.Add(ParseItem(path, rawItems[j], vocabulary, $"{where}.items[{j}]"));
        }

        if (items.Count == 0)
        {
            throw new BoardFileException(path, $"{where}.items: must contain at least one entry");
        }

        StateInfo state;
        if (!string.IsNullOrWhiteSpace(s?.State))
        {
            state = Resolve(path, vocabulary, s!.State!.Trim(), $"{where}.state");
        }
        else if (States.WorstOf(items) is { } rolled)
        {
            state = rolled;
        }
        else
        {
            throw new BoardFileException(
                path, $"{where}: 'state' required (or at least one item with a state)");
        }

        return new Section
        {
            Name = name!,
            State = state,
            About = TrimOrNull(s?.About),
            Items = items,
            Next = ParseStrings(path, s?.Next, $"{where}.next"),
            WaitingOn = TrimOrNull(s?.WaitingOn),
            Links = ParseLinks(path, s?.Links, $"{where}.links"),
            Updated = updated,
            UpdatedUtc = updatedUtc,
            StaleAfterHours = ttl
        };
    }

    private static BoardItem ParseItem(
        string path, SchemaItem? item, IReadOnlyDictionary<string, StateInfo> vocabulary, string where)
    {
        var name = TrimOrNull(item?.Name);
        var note = TrimOrNull(item?.Note);
        var tag = TrimOrNull(item?.Tag);

        if (tag is not null)
        {
            if (tag.Length > 16)
            {
                throw new BoardFileException(path, $"{where}.tag: at most 16 characters, got {tag.Length}");
            }

            if (note is null)
            {
                throw new BoardFileException(path, $"{where}: 'tag' requires a 'note'");
            }
        }

        if (name is null && note is null)
        {
            throw new BoardFileException(path, $"{where}: needs 'name' or 'note'");
        }

        var state = string.IsNullOrWhiteSpace(item?.State)
            ? null
            : Resolve(path, vocabulary, item!.State!.Trim(), $"{where}.state");

        return new BoardItem(name, state, note, tag);
    }

    private static IReadOnlyList<string> ParseStrings(string path, object? value, string where)
    {
        switch (value)
        {
            case null:
                return [];
            case string s:
                return string.IsNullOrWhiteSpace(s)
                    ? throw new BoardFileException(path, $"{where}: must be a non-empty string or list")
                    : [s.Trim()];
            case IEnumerable<object> list:
                var result = new List<string>();
                foreach (var entry in list)
                {
                    if (entry is not string e || string.IsNullOrWhiteSpace(e))
                    {
                        throw new BoardFileException(
                            path, $"{where}: must be a string or a list of non-empty strings");
                    }

                    result.Add(e.Trim());
                }

                return result;
            default:
                throw new BoardFileException(path, $"{where}: must be a string or a list of strings");
        }
    }

    private static IReadOnlyList<Link> ParseLinks(string path, List<LinkSchema>? links, string where)
    {
        if (links is null || links.Count == 0)
        {
            return [];
        }

        var parsed = new List<Link>(links.Count);
        for (var j = 0; j < links.Count; j++)
        {
            var label = TrimOrNull(links[j]?.Label);
            var href = TrimOrNull(links[j]?.Href);
            if (label is null)
            {
                throw new BoardFileException(
                    path, $"{where}[{j}].label: required and must be a non-empty string");
            }

            if (href is null)
            {
                throw new BoardFileException(
                    path, $"{where}[{j}].href: required and must be a non-empty string");
            }

            if (!HasAllowedScheme(href))
            {
                throw new BoardFileException(
                    path, $"{where}[{j}].href: only http, https, mailto or relative URLs are " +
                    $"allowed, got '{href}'");
            }

            parsed.Add(new Link(label, href));
        }

        return parsed;
    }

    /// <summary>
    /// Relative URLs and http/https/mailto only: a ':' before any '/', '?' or '#'
    /// marks a scheme, and anything but the allowlist (e.g. javascript:, data:)
    /// is rejected at load time.
    /// </summary>
    private static bool HasAllowedScheme(string href)
    {
        var i = href.IndexOfAny([':', '/', '?', '#']);
        if (i < 0 || href[i] != ':')
        {
            return true;
        }

        return href[..i].ToLowerInvariant() is "http" or "https" or "mailto";
    }

    private static IReadOnlyList<QueueEntry> ParseQueue(string path, List<QueueSchema>? queue)
    {
        if (queue is null || queue.Count == 0)
        {
            return [];
        }

        var parsed = new List<QueueEntry>(queue.Count);
        for (var i = 0; i < queue.Count; i++)
        {
            var name = TrimOrNull(queue[i]?.Name);
            if (name is null)
            {
                throw new BoardFileException(
                    path, $"queue[{i}].name: required and must be a non-empty string");
            }

            parsed.Add(new QueueEntry(name, TrimOrNull(queue[i]?.Note)));
        }

        return parsed;
    }

    private static IReadOnlyList<ClosedEntry> ParseClosed(string path, List<ClosedSchema>? closed)
    {
        if (closed is null || closed.Count == 0)
        {
            return [];
        }

        var parsed = new List<ClosedEntry>(closed.Count);
        for (var i = 0; i < closed.Count; i++)
        {
            var name = TrimOrNull(closed[i]?.Name);
            var verdict = TrimOrNull(closed[i]?.Verdict);
            var date = TrimOrNull(closed[i]?.Date);
            if (name is null || verdict is null)
            {
                throw new BoardFileException(
                    path, $"closed[{i}]: 'name' and 'verdict' are required non-empty strings");
            }

            if (date is null || !DateTime.TryParseExact(date, ClosedDateFormat,
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                throw new BoardFileException(
                    path, $"closed[{i}].date: must be 'YYYY-MM-DD', got '{date ?? "null"}'");
            }

            parsed.Add(new ClosedEntry(name, verdict, date));
        }

        return parsed;
    }

    private static int ParseTtl(string path, int? value, string where, int fallback)
    {
        if (value is null)
        {
            return fallback;
        }

        if (value is < 1)
        {
            throw new BoardFileException(path, $"{where}: must be a positive integer, got {value}");
        }

        return value.Value;
    }

    private static BoardTheme ParseTheme(string path, string? theme)
    {
        if (string.IsNullOrWhiteSpace(theme))
        {
            return BoardTheme.Auto;
        }

        return theme.Trim().ToLowerInvariant() switch
        {
            "auto" => BoardTheme.Auto,
            "light" => BoardTheme.Light,
            "dark" => BoardTheme.Dark,
            var t => throw new BoardFileException(
                path, $"'board.theme' must be one of auto|light|dark, got '{t}'")
        };
    }

    private static Board LoadLegacyBoard(
        string path, Schema schema, IReadOnlyDictionary<string, StateInfo> vocabulary)
    {
        if (schema.Board is not null || schema.Queue is not null || schema.Closed is not null)
        {
            throw new BoardFileException(
                path, "legacy flat schema (title + items): 'board', 'queue' and 'closed' are " +
                "not allowed here — use the v2 board schema (board.title + sections)");
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
                throw new BoardFileException(
                    path, $"items[{i}].name: required and must be a non-empty string");
            }

            if (!string.IsNullOrWhiteSpace(item?.Tag))
            {
                throw new BoardFileException(
                    path, $"items[{i}].tag: not supported in the legacy flat schema (facts with " +
                    "tags need a v2 section board)");
            }

            var state = Resolve(path, vocabulary, item?.State?.Trim(), $"items[{i}].state");

            var note = item.Note is { } n && !string.IsNullOrWhiteSpace(n) ? n.Trim() : null;
            parsed.Add(new BoardItem(name, state, note));
        }

        return new Board(title!, parsed);
    }

    private static string? TrimOrNull(string? value) =>
        value is null || string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int CountDocuments(string path, string text)
    {
        try
        {
            var stream = new YamlStream();
            stream.Load(new StringReader(text));
            return stream.Documents.Count;
        }
        catch (YamlException ex)
        {
            throw new BoardFileException(path, $"invalid content at line {ex.Start.Line}: {ex.Message}");
        }
    }

    private sealed class Schema
    {
        public string? Title { get; set; }

        public List<SchemaItem>? Items { get; set; }

        public BoardSchema? Board { get; set; }

        public Dictionary<string, StateSchema>? States { get; set; }

        public List<SectionSchema>? Sections { get; set; }

        public List<QueueSchema>? Queue { get; set; }

        public List<ClosedSchema>? Closed { get; set; }
    }

    private sealed class BoardSchema
    {
        public string? Title { get; set; }

        public string? Description { get; set; }

        public string? Footer { get; set; }

        public int? StaleAfterHours { get; set; }

        public string? Theme { get; set; }
    }

    private sealed class StateSchema
    {
        public string? Label { get; set; }

        public int? Rank { get; set; }

        public string? Color { get; set; }
    }

    private sealed class SectionSchema
    {
        public string? Name { get; set; }

        public string? About { get; set; }

        public string? State { get; set; }

        public string? Updated { get; set; }

        public int? StaleAfterHours { get; set; }

        public List<SchemaItem>? Items { get; set; }

        public object? Next { get; set; }

        public string? WaitingOn { get; set; }

        public List<LinkSchema>? Links { get; set; }
    }

    private sealed class SchemaItem
    {
        public string? Name { get; set; }

        public string? State { get; set; }

        public string? Note { get; set; }

        public string? Tag { get; set; }
    }

    private sealed class LinkSchema
    {
        public string? Label { get; set; }

        public string? Href { get; set; }
    }

    private sealed class QueueSchema
    {
        public string? Name { get; set; }

        public string? Note { get; set; }
    }

    private sealed class ClosedSchema
    {
        public string? Name { get; set; }

        public string? Verdict { get; set; }

        public string? Date { get; set; }
    }
}
