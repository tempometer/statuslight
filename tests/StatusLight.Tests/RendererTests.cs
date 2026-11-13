using StatusLight;

namespace StatusLight.Tests;

public sealed class RendererTests
{
    private static readonly DateTimeOffset GeneratedAt =
        new(2026, 11, 13, 20, 4, 24, TimeSpan.Zero);

    [Fact]
    public void Render_ContainsEscapedTitleInHeadAndBody()
    {
        var board = new Board("<R&D> board", [new BoardItem("A", BoardState.Green)]);

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("&lt;R&amp;D&gt; board", html);
        Assert.DoesNotContain("<R&D>", html);
        Assert.Equal(2, CountOccurrences(html, "&lt;R&amp;D&gt; board")); // <title> + <h1>
    }

    [Fact]
    public void Render_OneListItemPerItem()
    {
        var board = new Board("T", new List<BoardItem>
        {
            new("API", BoardState.Green),
            new("Worker", BoardState.Yellow, "lag"),
            new("Billing", BoardState.Red),
        });

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Equal(3, CountOccurrences(html, "<li>"));
        Assert.Contains("API", html);
        Assert.Contains("Worker", html);
        Assert.Contains("Billing", html);
    }

    [Fact]
    public void Render_EscapesItemNameAndNote()
    {
        var board = new Board("T", [new BoardItem("<script>alert(1)</script>", BoardState.Green, "a < b & c")]);

        var html = Renderer.Render(board, GeneratedAt);

        Assert.DoesNotContain("<script>alert", html);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html);
        Assert.Contains("a &lt; b &amp; c", html);
    }

    [Fact]
    public void Render_StateShownAsTextAndColor()
    {
        var board = new Board("T", new List<BoardItem>
        {
            new("A", BoardState.Green),
            new("B", BoardState.Yellow),
            new("C", BoardState.Red),
        });

        var html = Renderer.Render(board, GeneratedAt);

        // colour dot for sighted users, text label for colour-blind / screen readers
        Assert.Contains("class=\"dot dot-green\"", html);
        Assert.Contains("class=\"dot dot-yellow\"", html);
        Assert.Contains("class=\"dot dot-red\"", html);
        Assert.Contains("<span class=\"state st-green\">green</span>", html);
        Assert.Contains("<span class=\"state st-yellow\">yellow</span>", html);
        Assert.Contains("<span class=\"state st-red\">red</span>", html);
    }

    [Fact]
    public void Render_HasViewportMetaTag()
    {
        var board = new Board("T", [new BoardItem("A", BoardState.Green)]);

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">", html);
    }

    [Fact]
    public void Render_MultiLineNoteKeepsLineBreaks()
    {
        var board = new Board("T", [new BoardItem("A", BoardState.Green, "line one\nline two")]);

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("line one\nline two", html);
        Assert.Contains("white-space: pre-line", html);
    }

    [Fact]
    public void Render_FooterHasIso8601UtcTimestamp()
    {
        var board = new Board("T", [new BoardItem("A", BoardState.Green)]);

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Contains("Generated: 2026-11-13T20:04:24Z", html);
    }

    [Fact]
    public void Render_NoteSpanOnlyWhenNotePresent()
    {
        var board = new Board("T", new List<BoardItem>
        {
            new("A", BoardState.Green, "has note"),
            new("B", BoardState.Green),
        });

        var html = Renderer.Render(board, GeneratedAt);

        Assert.Equal(1, CountOccurrences(html, "class=\"note\""));
        Assert.Contains("has note", html);
    }

    [Fact]
    public void Render_IsSelfContainedDocument()
    {
        var board = new Board("T", [new BoardItem("A", BoardState.Green)]);

        var html = Renderer.Render(board, GeneratedAt);

        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("<meta charset=\"utf-8\">", html);
        Assert.Contains("<style>", html);
        Assert.DoesNotContain("http://", html);
        Assert.DoesNotContain("https://", html);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
