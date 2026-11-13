using System.Net;
using System.Text;

namespace StatusLight;

/// <summary>
/// Renders a <see cref="Board"/> as a single self-contained HTML page
/// (inline CSS, no external assets). All text is HTML-escaped.
/// </summary>
public static class Renderer
{
    public static string Render(Board board, DateTimeOffset generatedAtUtc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine($"<title>{Escape(board.Title)}</title>");
        sb.AppendLine("<style>");
        sb.Append(Styles);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<main class=\"board\">");
        sb.AppendLine($"<h1>{Escape(board.Title)}</h1>");
        sb.AppendLine("<ul>");
        foreach (var item in board.Items)
        {
            var note = item.Note is null
                ? string.Empty
                : $"<span class=\"note\">{Escape(item.Note)}</span>";
            sb.AppendLine(
                $"  <li><span class=\"chip chip-{CssClass(item.State)}\"></span>" +
                $"<span class=\"name\">{Escape(item.Name)}</span>{note}</li>");
        }

        sb.AppendLine("</ul>");
        sb.AppendLine($"<footer>Generated: {generatedAtUtc.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}</footer>");
        sb.AppendLine("</main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string CssClass(BoardState state) => state switch
    {
        BoardState.Green => "green",
        BoardState.Yellow => "yellow",
        BoardState.Red => "red",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
    };

    private static string Escape(string value) => WebUtility.HtmlEncode(value);

    private const string Styles = """
        :root { color-scheme: light dark; }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: system-ui, sans-serif; background: #f5f5f7; color: #1d1d1f; }
        .board { max-width: 40rem; margin: 3rem auto; padding: 0 1rem; }
        h1 { font-size: 1.5rem; }
        ul { list-style: none; padding: 0; display: flex; flex-direction: column; gap: .5rem; }
        li { display: flex; align-items: center; gap: .75rem; background: #fff; border: 1px solid #e5e5ea; border-radius: .5rem; padding: .6rem .9rem; }
        .chip { width: 1rem; height: 1rem; border-radius: 50%; flex: none; }
        .chip-green { background: #34c759; }
        .chip-yellow { background: #ffcc00; }
        .chip-red { background: #ff3b30; }
        .name { font-weight: 600; }
        .note { color: #6e6e73; }
        footer { margin-top: 1.5rem; color: #8e8e93; font-size: .8rem; }
        @media (prefers-color-scheme: dark) {
          body { background: #1c1c1e; color: #f5f5f7; }
          li { background: #2c2c2e; border-color: #3a3a3c; }
          .note { color: #a1a1a6; }
        }
        """;
}
