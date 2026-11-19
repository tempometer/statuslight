using System.Globalization;
using StatusLight;

namespace StatusLight.Tests;

public sealed class CardsRendererTests
{
    private static readonly DateTimeOffset GeneratedAt =
        new(2026, 11, 18, 20, 15, 0, TimeSpan.Zero);

    private static Board Board(params Section[] sections) => new()
    {
        Title = "Aurora",
        Sections = sections
    };

    private static Section Section(
        string name = "Payments",
        string? stateKey = null,
        string about = "Payments platform — cards, SEPA, payouts",
        BoardItem[]? items = null,
        string[]? next = null,
        string? waitingOn = null,
        Link[]? links = null,
        string updated = "2026-11-18 09:12",
        int staleAfter = 48) => new()
    {
        Name = name,
        State = new StateInfo(stateKey ?? "green", "on plan", 0, "#34c759"),
        About = about,
        Items = items ?? [new BoardItem("API", States.Green, "v2.4.1")],
        Next = next ?? [],
        WaitingOn = waitingOn,
        Links = links ?? [],
        Updated = updated,
        UpdatedUtc = DateTime.ParseExact(updated, "yyyy-MM-dd HH:mm",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
        StaleAfterHours = staleAfter
    };

    [Fact]
    public void Render_TitleDescriptionAndGenerated()
    {
        var board = new Board
        {
            Title = "<Aurora>",
            Description = "service health",
            Sections = [Section()]
        };

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("<h1>&lt;Aurora&gt;</h1>", html);
        Assert.Contains("service health", html);
        Assert.Contains("Generated: 2026-11-18T20:15:00Z", html);
        Assert.DoesNotContain("<Aurora>", html);
    }

    [Fact]
    public void Render_CardHeadWithDotNameAndStateLabel()
    {
        var board = Board(Section());

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("style=\"--c:#34c759\"", html);
        Assert.Contains("<span class=\"name\">Payments</span>", html);
        Assert.Contains("<span class=\"slabel\">on plan</span>", html);
    }

    [Fact]
    public void Render_AboutBoldsFullNameBeforeEmDash()
    {
        var board = Board(Section());

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("<b>Payments platform</b> — cards, SEPA, payouts", html);
    }

    [Fact]
    public void Render_StatefulItemAsTrafficRow()
    {
        var board = Board(Section(items: [new BoardItem("API", States.Red, "5xx spiking")]));

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("<span class=\"iname\">API</span>", html);
        Assert.Contains("5xx spiking", html);
        Assert.Contains("style=\"color:#ff3b30\"", html); // label color conveys state
        Assert.Contains(">red</span>", html); // text conveys state
    }

    [Fact]
    public void Render_FactItemWithTagChip_Escaped()
    {
        var board = Board(Section(items:
            [new BoardItem(null, null, "<lag> 4m & rising", "RADAR<A>")]));

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("<span class=\"tag\">RADAR&lt;A&gt;</span>", html);
        Assert.Contains("&lt;lag&gt; 4m &amp; rising", html);
        Assert.DoesNotContain("<lag>", html);
    }

    [Fact]
    public void Render_NextSteps_SingleAndMultiple()
    {
        var single = Renderer.Render(Board(Section(next: ["rotate keys"])), GeneratedAt);
        Assert.Contains("next</span> rotate keys", single);

        var html = Renderer.Render(Board(Section(next: ["rotate keys", "cut release"])), GeneratedAt);
        Assert.Contains("next steps", html);
        Assert.Contains("<div class=\"nxt\">rotate keys</div>", html);
        Assert.Contains("<div class=\"nxt\">cut release</div>", html);
    }

    [Fact]
    public void Render_WaitingOn()
    {
        var html = Renderer.Render(Board(Section(waitingOn: "security review")), GeneratedAt);

        Assert.Contains("waiting on</span> security review", html);
    }

    [Fact]
    public void Render_LinkChips_Escaped()
    {
        var html = Renderer.Render(
            Board(Section(links: [new Link("docs & runbook", "docs/x.md")])), GeneratedAt);

        Assert.Contains("<a href=\"docs/x.md\">docs &amp; runbook</a>", html);
    }

    [Fact]
    public void Render_QueueAndClosedLanes()
    {
        var board = new Board
        {
            Title = "Aurora",
            Sections = [Section()],
            Queue = [new QueueEntry("Legacy sync", "waiting on vendor")],
            Closed = [new ClosedEntry("Billing v1", "replaced", "2026-11-01")]
        };

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("<h2>Queue</h2>", html);
        Assert.Contains("<b>Legacy sync</b>", html);
        Assert.Contains("waiting on vendor", html);
        Assert.Contains("<h2>Closed</h2>", html);
        Assert.Contains("✅ Billing v1", html);
        Assert.Contains("replaced (2026-11-01)", html);
    }

    [Fact]
    public void Render_CustomFooter()
    {
        var board = new Board { Title = "Aurora", Sections = [Section()], Footer = "one row per stream" };

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("<footer>one row per stream</footer>", html);
    }

    [Fact]
    public void Render_UpdatedEmitsUtcIsoWithDisplay()
    {
        var html = Renderer.Render(Board(Section(updated: "2026-11-18 09:12")), GeneratedAt);

        // exact instant in data-upd, human display in data-disp: every viewer sees the same age
        Assert.Contains("data-upd=\"2026-11-18T09:12:00Z\"", html);
        Assert.Contains("data-disp=\"2026-11-18 09:12 UTC\"", html);
    }

    [Fact]
    public void Render_UpdatedDrivesAgeScript()
    {
        var board = new Board
        {
            Title = "Aurora",
            Sections = [Section(updated: "2026-11-18 09:12", staleAfter: 24), Section(name: "Other", staleAfter: 72)]
        };

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("data-upd=\"2026-11-18T09:12:00Z\"", html);
        Assert.Contains("data-stale-after=\"24\"", html);
        Assert.Contains("data-stale-after=\"72\"", html);
        Assert.Contains(".updated.stale", html); // stale styling exists
        Assert.Contains("future timestamp", html); // future dates are flagged, not clamped
        Assert.Contains("diff > lim * 3600000", html); // threshold on exact ms, not rounded hours
        Assert.Contains("querySelectorAll('.updated[data-upd]')", html); // age script wired
    }

    [Fact]
    public void Render_GridFitsPhoneViewports()
    {
        var html = Renderer.Render(Board(Section()), GeneratedAt);

        Assert.Contains("minmax(min(24rem,100%),1fr)", html);
    }

    [Fact]
    public void Render_MultiLineNotesKeepBreaksEverywhere()
    {
        var html = Renderer.Render(
            Board(Section(items: [new BoardItem("API", States.Red, "line one\nline two")])), GeneratedAt);

        Assert.Contains(".inote{color:var(--dim);white-space:pre-line}", html);
        Assert.Contains("line one\nline two", html);
    }

    [Fact]
    public void Render_FactItemWithNameLeadIn()
    {
        var only = Renderer.Render(
            Board(Section(items: [new BoardItem("OnlyName")])), GeneratedAt);
        Assert.Contains("<span class=\"iname\">OnlyName</span>", only);

        var both = Renderer.Render(
            Board(Section(items: [new BoardItem("SEPA", Note: "maintenance window")])), GeneratedAt);
        Assert.Contains("<span class=\"iname\">SEPA</span>", both);
        Assert.Contains("<span class=\"txt\">maintenance window</span>", both);
    }

    [Fact]
    public void Render_StatefulItemKeepsTagChip()
    {
        var html = Renderer.Render(
            Board(Section(items: [new BoardItem("API", States.Red, "5xx", "TAGX")])), GeneratedAt);

        Assert.Contains("<span class=\"tag\">TAGX</span>", html);
    }

    [Fact]
    public void Render_ThemeAuto_DarkBlockHasRootSelectorAndBalancedBraces()
    {
        var html = Renderer.Render(
            new Board { Title = "T", Theme = BoardTheme.Auto, Sections = [Section()] }, GeneratedAt);

        Assert.Contains("@media (prefers-color-scheme: dark){:root{", html);
        var opens = html.Count(c => c == '{');
        var closes = html.Count(c => c == '}');
        Assert.Equal(opens, closes); // no stray brace dropping the next rule
    }

    [Fact]
    public void Render_ThemeAuto_HasDarkMediaQuery()
    {
        var html = Renderer.Render(new Board { Title = "T", Theme = BoardTheme.Auto, Sections = [Section()] }, GeneratedAt);

        Assert.Contains("prefers-color-scheme: dark", html);
    }

    [Fact]
    public void Render_ThemeDark_NoMediaQuery()
    {
        var html = Renderer.Render(new Board { Title = "T", Theme = BoardTheme.Dark, Sections = [Section()] }, GeneratedAt);

        Assert.DoesNotContain("prefers-color-scheme", html);
        Assert.Contains("--bg:#0f172a", html); // dark palette as root
    }

    [Fact]
    public void Render_SelfContained_NoExternalAssets()
    {
        var board = new Board
        {
            Title = "Aurora",
            Sections = [Section(links: [new Link("site", "https://example.com/x")])],
            Queue = [new QueueEntry("Q")]
        };

        var html = Renderer.Render(board, GeneratedAt);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<link ", html);
        Assert.DoesNotContain("src=\"http", html); // no external scripts
        Assert.DoesNotContain("url(http", html);   // no external css assets
        Assert.Contains("href=\"https://example.com/x\"", html); // links are fine
    }
}
