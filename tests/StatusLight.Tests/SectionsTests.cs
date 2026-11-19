using StatusLight;

namespace StatusLight.Tests;

public sealed class SectionsTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "statuslight-sections-" + Guid.NewGuid().ToString("N"));

    private string WriteYaml(string content)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "board.yml");
        File.WriteAllText(path, content);
        return path;
    }

    private static string Section(string body) =>
        """
        board:
          title: Aurora
        states:
          green:  { label: "on plan" }
          yellow: { label: "needs decision", rank: 1 }
          red:    { label: "broken", rank: 2 }
          gray:   { label: "parked" }
          closed: { label: "done, with verdict" }
        sections:
        """ + "\n" + body + "\n";

    [Fact]
    public void Load_MinimalSectionBoard()
    {
        var path = WriteYaml(Section("""
            - name: Payments
              state: green
              updated: "2026-11-18 09:12"
              items:
                - { name: API, state: green }
            """));

        var board = BoardFile.Load(path);

        Assert.False(board.IsLegacyList);
        Assert.Equal("Aurora", board.Title);
        var section = Assert.Single(board.Sections);
        Assert.Equal("Payments", section.Name);
        Assert.Equal(States.Green.Key, section.State.Key);
        Assert.Equal("on plan", section.State.Label);
        Assert.Equal("2026-11-18 09:12", section.Updated);
        Assert.Equal(48, section.StaleAfterHours);
    }

    [Fact]
    public void Load_SectionStateRollsUpToWorstItem()
    {
        var path = WriteYaml(Section("""
            - name: Mixed
              updated: "2026-11-18 09:12"
              items:
                - { name: A, state: green }
                - { name: B, state: yellow }
                - { name: C, state: green }
            """));

        var board = BoardFile.Load(path);

        Assert.Equal("yellow", board.Sections[0].State.Key);
    }

    [Fact]
    public void Load_ExplicitStateOverridesRollup()
    {
        var path = WriteYaml(Section("""
            - name: Watch
              state: gray
              updated: "2026-11-18 09:12"
              items:
                - { name: A, state: red }
            """));

        var board = BoardFile.Load(path);

        Assert.Equal("gray", board.Sections[0].State.Key);
        Assert.Equal(0, board.Sections[0].State.Rank); // gray default rank 0
    }

    [Fact]
    public void Load_CustomRankDrivesRollup()
    {
        var path = WriteYaml("""
            board: { title: Aurora }
            states:
              ok:      { rank: 0 }
              degrade: { rank: 5 }
            sections:
              - name: S
                updated: "2026-11-18 09:12"
                items:
                  - { name: A, state: ok }
                  - { name: B, state: degrade }
            """);

        var board = BoardFile.Load(path);

        Assert.Equal("degrade", board.Sections[0].State.Key);
        Assert.Equal(States.FallbackColor, board.Sections[0].State.Color);
    }

    [Fact]
    public void Load_SectionWithoutStateOrStatefulItems_Rejected()
    {
        var path = WriteYaml(Section("""
            - name: Facts
              updated: "2026-11-18 09:12"
              items:
                - { note: just a fact }
            """));

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("sections[0]: 'state' required", ex.Message);
    }

    [Fact]
    public void Load_UnknownSectionState_ListsKnownKeys()
    {
        var path = WriteYaml(Section("""
            - name: X
              state: violet
              updated: "2026-11-18 09:12"
              items:
                - { name: A, state: green }
            """));

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("unknown state 'violet'", ex.Message);
        Assert.Contains("green", ex.Message);
        Assert.Contains("gray", ex.Message);
    }

    [Fact]
    public void Load_BadColor_Rejected()
    {
        var path = WriteYaml("""
            board: { title: Aurora }
            states:
              weird: { rank: 1, color: "#12345" }
            sections:
              - name: S
                state: weird
                updated: "2026-11-18 09:12"
                items: [{ note: x }]
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("states.weird.color", ex.Message);
        Assert.Contains("#RRGGBB", ex.Message);
    }

    [Fact]
    public void Load_MutuallyExclusiveItemsAndSections_Rejected()
    {
        var path = WriteYaml("""
            board: { title: Aurora }
            title: legacy
            items: [{ name: A, state: green }]
            sections:
              - name: S
                state: green
                updated: "2026-11-18 09:12"
                items: [{ name: B, state: green }]
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("not both", ex.Message);
    }

    [Fact]
    public void Load_MissingBoardTitle_Rejected()
    {
        var path = WriteYaml("""
            sections:
              - name: S
                state: green
                updated: "2026-11-18 09:12"
                items: [{ name: A, state: green }]
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("board.title", ex.Message);
    }

    [Theory]
    [InlineData("2026-11-18")]
    [InlineData("18.11.2026 09:12")]
    [InlineData("2026-13-18 09:12")]
    [InlineData("")]
    public void Load_BadUpdatedFormat_Rejected(string updated)
    {
        var path = WriteYaml(Section($"""
            - name: S
              state: green
              updated: "{updated}"
              items:
                - name: A
                  state: green
            """));

        Assert.Throws<BoardFileException>(() => BoardFile.Load(path));
    }

    [Fact]
    public void Load_UpdatedNeedsDateAndTime()
    {
        var path = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18"
              items: [{ name: A, state: green }]
            """));

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("date AND time", ex.Message);
    }

    [Fact]
    public void Load_TagTooLong_Rejected()
    {
        var path = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items:
                - { tag: VERYLONGTAGNAME123, note: x }
            """));

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("items[0].tag", ex.Message);
        Assert.Contains("16", ex.Message);
    }

    [Fact]
    public void Load_TagWithoutNote_Rejected()
    {
        var path = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items:
                - { tag: RADAR }
            """));

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("'tag' requires a 'note'", ex.Message);
    }

    [Fact]
    public void Load_ItemWithoutNameAndNote_Rejected()
    {
        var path = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items:
                - state: green
            """));

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("needs 'name' or 'note'", ex.Message);
    }

    [Fact]
    public void Load_NextAcceptsStringOrList_RejectsOtherTypes()
    {
        var single = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
              next: one step
            """));
        Assert.Equal(["one step"], BoardFile.Load(single).Sections[0].Next);

        var many = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
              next: [a, b]
            """));
        Assert.Equal(["a", "b"], BoardFile.Load(many).Sections[0].Next);

        var bad = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
              next: { oops: yes }
            """));
        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(bad));
        Assert.Contains("sections[0].next", ex.Message);
    }

    [Fact]
    public void Load_LinksNeedLabelAndHref()
    {
        var ok = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
              links: [{ label: docs, href: docs/x.md }]
            """));
        Assert.Equal(new Link("docs", "docs/x.md"), Assert.Single(BoardFile.Load(ok).Sections[0].Links));

        var bad = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
              links: [{ label: docs }]
            """));
        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(bad));
        Assert.Contains("links[0].href", ex.Message);
    }

    [Fact]
    public void Load_QueueAndClosed_Validated()
    {
        var ok = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
            """) + """
            queue:
              - { name: Later, note: parked }
            closed:
              - { name: Old, verdict: replaced, date: "2026-11-01" }
            """);
        var board = BoardFile.Load(ok);
        Assert.Equal(new QueueEntry("Later", "parked"), Assert.Single(board.Queue));
        Assert.Equal(new ClosedEntry("Old", "replaced", "2026-11-01"), Assert.Single(board.Closed));

        var bad = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
            """) + """
            closed:
              - { name: Old, verdict: replaced, date: "01.11.2026" }
            """);
        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(bad));
        Assert.Contains("closed[0].date", ex.Message);
    }

    [Fact]
    public void Load_ThemeValidated()
    {
        var ok = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
            """).Replace("board:\n  title: Aurora", "board:\n  title: Aurora\n  theme: dark"));
        Assert.Equal(BoardTheme.Dark, BoardFile.Load(ok).Theme);

        var bad = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
            """).Replace("board:\n  title: Aurora", "board:\n  title: Aurora\n  theme: blue"));
        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(bad));
        Assert.Contains("theme", ex.Message);
    }

    [Fact]
    public void Load_StaleTtl_ValidatedAndInherited()
    {
        var yml = Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              stale_after_hours: 24
              items: [{ name: A, state: green }]
            - name: T
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: B, state: green }]
            """).Replace("board:\n  title: Aurora", "board:\n  title: Aurora\n  stale_after_hours: 72");
        var board = BoardFile.Load(WriteYaml(yml));

        Assert.Equal(24, board.Sections[0].StaleAfterHours);
        Assert.Equal(72, board.Sections[1].StaleAfterHours);
    }

    [Fact]
    public void Load_LegacyItemsWithCustomStates_Allowed()
    {
        var path = WriteYaml("""
            title: T
            states:
              green: { label: fine }
              gray:  { label: parked }
            items:
              - name: A
                state: gray
            """);

        var board = BoardFile.Load(path);

        Assert.True(board.IsLegacyList);
        Assert.Equal("parked", board.Sections[0].Items[0].State!.Label);
    }

    [Fact]
    public void Load_FactItemWithTag_Parsed()
    {
        var path = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items:
                - name: API
                  state: green
                  note: v2.4.1
                - tag: RADAR
                  note: 3 hits today
            """));

        var board = BoardFile.Load(path);

        var items = board.Sections[0].Items;
        Assert.Equal("API", items[0].Name);
        Assert.Equal("green", items[0].State!.Key);
        Assert.Equal("on plan", items[0].State.Label); // label comes from the custom vocabulary
        Assert.Equal("v2.4.1", items[0].Note);
        Assert.Equal(new BoardItem(null, null, "3 hits today", "RADAR"), items[1]);
    }

    [Fact]
    public void Load_CustomStateWithoutRank_Rejected()
    {
        var path = WriteYaml("""
            board: { title: Aurora }
            states:
              green:   {}
              blocked: { label: Blocked }
            sections:
              - name: S
                updated: "2026-11-18 09:12"
                items:
                  - { name: A, state: green }
                  - { name: B, state: blocked }
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("states.blocked.rank", ex.Message);
        Assert.Contains("required for non-stock", ex.Message);
    }

    [Fact]
    public void Load_CaseVariantDuplicateStates_Rejected()
    {
        var path = WriteYaml("""
            board: { title: Aurora }
            states:
              OK: { rank: 0 }
              ok: { rank: 2, color: "#ff0000" }
            sections:
              - name: S
                updated: "2026-11-18 09:12"
                items: [{ name: A, state: ok }]
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("duplicate key", ex.Message);
        Assert.Contains("case-insensitive", ex.Message);
    }

    [Fact]
    public void Load_V2WithEmptySections_GoodError()
    {
        var path = WriteYaml("""
            board:
              title: Aurora
            sections:
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("'sections' must contain at least one entry", ex.Message);
    }

    [Fact]
    public void Load_V2RejectsLegacyTopLevelTitle()
    {
        var path = WriteYaml("""
            board:
              title: Aurora
            title: leftover
            sections:
              - name: S
                state: green
                updated: "2026-11-18 09:12"
                items: [{ name: A, state: green }]
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("board.title", ex.Message);
    }

    [Fact]
    public void Load_CapitalizedStockKeys_KeepStockRankAndColor()
    {
        var path = WriteYaml("""
            board: { title: Aurora }
            states:
              Green: { label: ok }
              Red:   { label: broken }
            sections:
              - name: S
                updated: "2026-11-18 09:12"
                items:
                  - { name: A, state: Red }
                  - { name: B, state: Green }
            """);

        var board = BoardFile.Load(path);

        // Red keeps stock rank 2 and colour even with a capitalised key
        Assert.Equal("broken", board.Sections[0].State.Label);
        Assert.Equal(2, board.Sections[0].State.Rank);
        Assert.Equal("#ff3b30", board.Sections[0].State.Color);
    }

    [Fact]
    public void Load_RollupTieBreaksByDeclarationOrder()
    {
        var path = WriteYaml(Section("""
            - name: Mixed
              updated: "2026-11-18 09:12"
              items:
                - { name: A, state: green }
                - { name: B, state: closed }
            - name: Parked
              updated: "2026-11-18 09:12"
              items:
                - { name: C, state: green }
                - { name: D, state: gray }
            """));

        var board = BoardFile.Load(path);

        // green, gray and closed all rank 0; declaration order (best-first) decides
        Assert.Equal("green", board.Sections[0].State.Key);
        Assert.Equal("green", board.Sections[1].State.Key);
    }

    [Fact]
    public void Load_LinkSchemeAllowlist()
    {
        var ok = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items: [{ name: A, state: green }]
              links:
                - { label: web, href: "https://x.example" }
                - { label: mail, href: "mailto:ops@example.com" }
                - { label: rel, href: docs/x.md }
                - { label: anchor, href: "#payments" }
            """));
        Assert.Equal(4, BoardFile.Load(ok).Sections[0].Links.Count);

        foreach (var bad in new[] { "javascript:alert(1)", "data:text/html,x", "vbscript:x", " JavaScript:alert(1)" })
        {
            var path = WriteYaml(Section($"""
                - name: S
                  state: green
                  updated: "2026-11-18 09:12"
                  items:
                    - name: A
                      state: green
                  links:
                    - label: x
                      href: "{bad}"
                """));
            var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));
            Assert.Contains("links[0].href", ex.Message);
        }
    }

    [Fact]
    public void Load_UnknownKeyInSection_RejectedNamesTheKey()
    {
        var path = WriteYaml(Section("""
            - name: S
              state: green
              updated: "2026-11-18 09:12"
              items:
                - name: A
                  state: green
                  tags: RADAR
            """));

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("tags", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
