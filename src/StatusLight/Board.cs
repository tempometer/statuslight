namespace StatusLight;

/// <summary>
/// A resolved state: key (as written in YAML), display label, severity rank, hex color,
/// and declaration order (the tie-break for rollups among equal ranks — declare
/// states best-first).
/// </summary>
public sealed record StateInfo(string Key, string Label, int Rank, string Color, int Order = 0);

public static class States
{
    public static readonly StateInfo Green = new("green", "green", 0, "#34c759", 0);
    public static readonly StateInfo Yellow = new("yellow", "yellow", 1, "#ffcc00", 1);
    public static readonly StateInfo Red = new("red", "red", 2, "#ff3b30", 2);
    public static readonly StateInfo Gray = new("gray", "gray", 0, "#9ca3af", 3);
    public static readonly StateInfo Closed = new("closed", "closed", 0, "#38bdf8", 4);

    /// <summary>v0.1 vocabulary: the default unless a custom <c>states:</c> block is given.</summary>
    public static readonly IReadOnlyDictionary<string, StateInfo> Defaults =
        new Dictionary<string, StateInfo>
        {
            [Green.Key] = Green,
            [Yellow.Key] = Yellow,
            [Red.Key] = Red
        };

    /// <summary>Color for a custom state that does not declare one.</summary>
    public const string FallbackColor = "#64748b";

    /// <summary>
    /// Stock state for a key (case-insensitive), or null. Single source of stock
    /// ranks and colours — loaders and renderers must go through this, not their
    /// own tables.
    /// </summary>
    public static StateInfo? Stock(string key) => key.Trim().ToLowerInvariant() switch
    {
        "green" => Green,
        "yellow" => Yellow,
        "red" => Red,
        "gray" => Gray,
        "closed" => Closed,
        _ => null
    };

    public static string CssSafe(string key)
    {
        var chars = key.Trim().ToLowerInvariant().Select(c =>
            char.IsLetterOrDigit(c) ? c : '-').ToArray();
        var safe = new string(chars).Trim('-');
        return safe.Length == 0 ? "state" : safe;
    }

    /// <summary>Worst (highest-rank) state among the items; null when none carries a state.</summary>
    public static StateInfo? WorstOf(IReadOnlyList<BoardItem> items) =>
        items
            .Select(i => i.State)
            .Where(s => s is not null)
            .OrderByDescending(s => s!.Rank)
            .ThenBy(s => s!.Order)
            .ThenBy(s => s!.Key, StringComparer.Ordinal)
            .FirstOrDefault();
}

public enum BoardTheme
{
    Auto,
    Light,
    Dark
}

public sealed record Link(string Label, string Href);

public sealed record QueueEntry(string Name, string? Note = null);

public sealed record ClosedEntry(string Name, string Verdict, string Date);

/// <summary>
/// One card row. With a <c>State</c> it renders as a traffic row; without one,
/// as a fact line (optionally prefixed with a <c>Tag</c> chip).
/// </summary>
public sealed record BoardItem(string? Name = null, StateInfo? State = null, string? Note = null, string? Tag = null);

public sealed record Section
{
    public required string Name { get; init; }

    /// <summary>Resolved state: explicit, or rolled up from items (worst rank).</summary>
    public required StateInfo State { get; init; }

    public string? About { get; init; }

    public IReadOnlyList<BoardItem> Items { get; init; } = [];

    public IReadOnlyList<string> Next { get; init; } = [];

    public string? WaitingOn { get; init; }

    public IReadOnlyList<Link> Links { get; init; } = [];

    /// <summary>Required for cards; 'YYYY-MM-DD HH:MM' (UTC) — display form.</summary>
    public string? Updated { get; init; }

    /// <summary>Parsed once at load (UTC); the renderer must not re-parse strings.</summary>
    public DateTime? UpdatedUtc { get; init; }

    public int StaleAfterHours { get; init; } = 48;
}

public sealed record Board
{
    public string Title { get; init; } = "";

    public IReadOnlyList<Section> Sections { get; init; } = [];

    public string? Description { get; init; }

    public string? Footer { get; init; }

    public BoardTheme Theme { get; init; } = BoardTheme.Auto;

    public int StaleAfterHours { get; init; } = 48;

    public IReadOnlyList<QueueEntry> Queue { get; init; } = [];

    public IReadOnlyList<ClosedEntry> Closed { get; init; } = [];

    /// <summary>True for the v0.1 flat schema — renders the original list layout.</summary>
    public bool IsLegacyList { get; init; }

    public Board()
    {
    }

    /// <summary>v0.1 convenience constructor: flat title + items as one implicit section.</summary>
    public Board(string title, IReadOnlyList<BoardItem> items)
    {
        Title = title;
        Sections = [new Section { Name = title, State = States.WorstOf(items) ?? States.Green, Items = items }];
        IsLegacyList = true;
    }
}
