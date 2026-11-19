using System.Globalization;
using System.Net;
using System.Text;

namespace StatusLight;

/// <summary>
/// Renders a <see cref="Board"/> as a single self-contained HTML page
/// (inline CSS, no external assets). All text is HTML-escaped, and every state
/// is conveyed by text as well as colour (colour-blind / screen-reader safe).
/// Legacy flat boards render the original list layout; section boards render
/// a card grid with client-side age and staleness badges.
/// </summary>
public static class Renderer
{
    public static string Render(Board board, DateTimeOffset generatedAtUtc) =>
        board.IsLegacyList
            ? RenderList(board, generatedAtUtc)
            : RenderCards(board, generatedAtUtc);

    // ---- v0.1 list layout (output unchanged) ----

    private static string RenderList(Board board, DateTimeOffset generatedAtUtc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine($"<title>{Escape(board.Title)}</title>");
        sb.AppendLine("<style>");
        sb.Append(ListStyles);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<main class=\"board\">");
        sb.AppendLine($"<h1>{Escape(board.Title)}</h1>");
        sb.AppendLine("<ul>");
        foreach (var item in board.Sections[0].Items)
        {
            var note = item.Note is null
                ? string.Empty
                : $"<span class=\"note\">{Escape(item.Note)}</span>";
            var dot = item.State is null
                ? string.Empty
                : $"<span class=\"dot dot-{States.CssSafe(item.State.Key)}\"{DotAttr(item.State)} aria-hidden=\"true\"></span>";
            var state = item.State is null
                ? string.Empty
                : $"<span class=\"state st-{States.CssSafe(item.State.Key)}\"{PillAttr(item.State)}>{Escape(item.State.Label)}</span>";
            sb.AppendLine(
                $"  <li>{dot}<span class=\"name\">{Escape(item.Name ?? "")}</span>{note}{state}</li>");
        }

        sb.AppendLine("</ul>");
        sb.AppendLine($"<footer>Generated: {generatedAtUtc.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}</footer>");
        sb.AppendLine("</main>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    // ---- v2 cards layout ----

    private static string RenderCards(Board board, DateTimeOffset generatedAtUtc)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        sb.AppendLine($"<title>{Escape(board.Title)}</title>");
        sb.AppendLine("<style>");
        sb.Append(CardsStyles(board.Theme));
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<main class=\"board\">");
        sb.AppendLine($"<h1>{Escape(board.Title)}</h1>");
        var sub = board.Description is null
            ? $"Generated: {generatedAtUtc.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}"
            : $"{Escape(board.Description)} · Generated: {generatedAtUtc.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss'Z'}";
        sb.AppendLine($"<div class=\"sub\">{sub}</div>");
        sb.AppendLine("<div class=\"grid\">");
        foreach (var section in board.Sections)
        {
            sb.Append(RenderCard(section));
        }

        sb.AppendLine("</div>");
        AppendChips(sb, "Queue", board.Queue,
            q => $"<span class=\"chip\"><b>{Escape(q.Name)}</b>" +
                 (q.Note is null ? "" : $" <span class=\"note\">— {Escape(q.Note)}</span>") + "</span>",
            "chips");
        AppendChips(sb, "Closed", board.Closed,
            c => $"<span class=\"chip\"><b>✅ {Escape(c.Name)}</b> " +
                 $"<span class=\"note\">— {Escape(c.Verdict)} ({Escape(c.Date)})</span></span>",
            "chips closed");
        if (board.Footer is not null)
        {
            sb.AppendLine($"<footer>{Escape(board.Footer)}</footer>");
        }

        sb.AppendLine("</main>");
        sb.AppendLine($"<script>{AgeScript}</script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string RenderCard(Section section)
    {
        var sb = new StringBuilder();
        sb.AppendLine(
            $"  <section class=\"card\" style=\"--c:{section.State.Color}\">");
        sb.AppendLine(
            $"    <div class=\"head\"><span class=\"dot\"></span><span class=\"name\">{Escape(section.Name)}</span>" +
            $"<span class=\"slabel\">{Escape(section.State.Label)}</span></div>");
        if (section.About is not null)
        {
            var head = section.About;
            var idx = head.IndexOf(" — ", StringComparison.Ordinal);
            var about = idx > 0
                ? $"<b>{Escape(head[..idx])}</b>{Escape(head[idx..])}"
                : Escape(head);
            sb.AppendLine($"    <div class=\"about\">{about}</div>");
        }

        sb.AppendLine("    <div class=\"body\">");
        foreach (var item in section.Items)
        {
            sb.AppendLine(RenderItem(item));
        }

        sb.AppendLine("    </div>");
        if (section.WaitingOn is not null)
        {
            sb.AppendLine(
                $"    <div class=\"waiting\"><span class=\"lbl\">waiting on</span> {Escape(section.WaitingOn)}</div>");
        }

        sb.AppendLine("    <div class=\"foot\">");
        if (section.Next.Count == 1)
        {
            sb.AppendLine(
                $"      <div class=\"nxt-one\"><span class=\"lbl\">next</span> {Escape(section.Next[0])}</div>");
        }
        else if (section.Next.Count > 1)
        {
            sb.AppendLine("      <span class=\"lbl\">next steps</span>");
            foreach (var step in section.Next)
            {
                sb.AppendLine($"      <div class=\"nxt\">{Escape(step)}</div>");
            }
        }

        if (section.Links.Count > 0)
        {
            var chips = string.Join(null, section.Links.Select(l =>
                $"<a href=\"{Escape(l.Href)}\">{Escape(l.Label)}</a>"));
            sb.AppendLine($"      <div class=\"links\">{chips}</div>");
        }

        sb.AppendLine(
            $"      <div class=\"updated\" data-upd=\"{IsoOf(section.UpdatedUtc, section.Updated)}\" " +
            $"data-disp=\"{Escape(section.Updated ?? "")} UTC\" " +
            $"data-stale-after=\"{section.StaleAfterHours}\">updated {Escape(section.Updated ?? "")} UTC</div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  </section>");
        return sb.ToString();
    }

    private static string RenderItem(BoardItem item)
    {
        if (item.State is not null)
        {
            var note = item.Note is null ? "" : $" <span class=\"inote\">{Escape(item.Note)}</span>";
            var tag = item.Tag is null
                ? string.Empty
                : $" <span class=\"tag\">{Escape(item.Tag)}</span>";
            return
                $"      <div class=\"row\"><span class=\"dot-sm\" style=\"background:{item.State.Color}\"></span>" +
                $"<span class=\"iname\">{Escape(item.Name ?? item.State.Label)}</span>{note}{tag}" +
                $"<span class=\"istate\" style=\"color:{item.State.Color}\">{Escape(item.State.Label)}</span></div>";
        }

        var factTag = item.Tag is null
            ? string.Empty
            : $"<span class=\"tag\">{Escape(item.Tag)}</span>";
        var name = item.Name is null
            ? string.Empty
            : $"<span class=\"iname\">{Escape(item.Name)}</span>";
        return $"      <div class=\"fact\">{factTag}{name}<span class=\"txt\">{Escape(item.Note)}</span></div>";
    }

    private static void AppendChips<T>(
        StringBuilder sb, string heading, IReadOnlyList<T> entries, Func<T, string> render, string cssClass)
    {
        if (entries.Count == 0)
        {
            return;
        }

        sb.AppendLine($"<h2>{heading}</h2>");
        sb.AppendLine($"<div class=\"{cssClass}\">");
        foreach (var entry in entries)
        {
            sb.AppendLine($"  {render(entry)}");
        }

        sb.AppendLine("</div>");
    }

    private static string Escape(string value) => WebUtility.HtmlEncode(value);

    /// <summary>
    /// Exact ISO-8601 instant from the DateTime parsed once at load. A null
    /// UpdatedUtc (model built by hand, never loaded) yields an empty attribute —
    /// the age script then skips the card instead of guessing.
    /// </summary>
    private static string IsoOf(DateTime? updatedUtc, string? raw) =>
        updatedUtc is { } utc
            ? utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
            : raw ?? "";

    /// <summary>
    /// True only for the v0.1 stock trio wearing its stock colour — the case where
    /// ListStyles classes already carry everything and v0.1 output stays
    /// byte-identical. gray/closed and custom states always get inline styles.
    /// States.Stock is the single source; no local colour table here.
    /// </summary>
    private static bool IsPlainV01Stock(StateInfo s) =>
        States.Stock(s.Key) is { } stock &&
        stock.Key is "green" or "yellow" or "red" &&
        stock.Color == s.Color;

    private static string DotAttr(StateInfo s) =>
        IsPlainV01Stock(s) ? "" : $" style=\"background:{s.Color}\"";

    private static string PillAttr(StateInfo s) =>
        IsPlainV01Stock(s) ? "" : $" style=\"color:{s.Color}\"";

    // ---- styles ----

    private const string ListStyles = """
        :root { color-scheme: light dark; }
        * { box-sizing: border-box; }
        body { margin: 0; font-family: system-ui, sans-serif; background: #f5f5f7; color: #1d1d1f; }
        .board { max-width: 40rem; margin: 3rem auto; padding: 0 1rem; }
        h1 { font-size: 1.5rem; }
        ul { list-style: none; padding: 0; display: flex; flex-direction: column; gap: .5rem; }
        li { display: flex; align-items: center; gap: .75rem; background: #fff; border: 1px solid #e5e5ea; border-radius: .5rem; padding: .6rem .9rem; flex-wrap: wrap; }
        .dot { width: 1rem; height: 1rem; border-radius: 50%; flex: none; }
        .dot-green { background: #34c759; }
        .dot-yellow { background: #ffcc00; }
        .dot-red { background: #ff3b30; }
        .name { font-weight: 600; }
        .note { color: #6e6e73; white-space: pre-line; }
        .state { margin-left: auto; padding: .15rem .5rem; border-radius: .3rem; font-size: .7rem; font-weight: 700; letter-spacing: .06em; text-transform: uppercase; }
        .st-green { color: #1d7a37; background: rgba(52,199,89,.15); }
        .st-yellow { color: #8a6d00; background: rgba(255,204,0,.18); }
        .st-red { color: #b3261e; background: rgba(255,59,48,.15); }
        footer { margin-top: 1.5rem; color: #8e8e93; font-size: .8rem; }
        @media (prefers-color-scheme: dark) {
          body { background: #1c1c1e; color: #f5f5f7; }
          li { background: #2c2c2e; border-color: #3a3a3c; }
          .note { color: #a1a1a6; }
          .st-green { color: #6ee798; background: rgba(52,199,89,.2); }
          .st-yellow { color: #ffde59; background: rgba(255,204,0,.2); }
          .st-red { color: #ff8a80; background: rgba(255,59,48,.2); }
        }
        """;

    private static string CardsStyles(BoardTheme theme)
    {
        const string lightVars = "--bg:#f5f5f7;--card:#ffffff;--text:#1d1d1f;--dim:#6e6e73;--line:#e5e5ea;--accent:#0071e3";
        const string darkVars = "--bg:#0f172a;--card:#1e293b;--text:#e2e8f0;--dim:#94a3b8;--line:#334155;--accent:#7dd3fc";
        var mode = theme switch
        {
            BoardTheme.Light => ":root{" + lightVars + "}",
            BoardTheme.Dark => ":root{" + darkVars + "}",
            _ => ":root{" + lightVars + "}\n@media (prefers-color-scheme: dark){:root{" + darkVars + "}}"
        };
        return mode + "\n" + CardBaseStyles;
    }

    private const string CardBaseStyles = """
        *{box-sizing:border-box}
        body{margin:0;font:15px/1.55 system-ui,'Segoe UI',sans-serif;background:var(--bg);color:var(--text);padding:24px 16px}
        .board{max-width:76rem;margin:0 auto}
        h1{font-size:1.4rem;margin:0 0 4px}
        .sub{color:var(--dim);font-size:.85rem;margin-bottom:18px}
        .grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(min(24rem,100%),1fr));gap:14px}
        .card{display:flex;flex-direction:column;background:var(--card);border:1px solid var(--line);border-left:5px solid var(--c,#888);border-radius:10px;padding:14px 16px}
        .head{display:flex;align-items:center;gap:9px;flex-wrap:wrap;margin-bottom:7px}
        .dot{width:12px;height:12px;border-radius:50%;background:var(--c,#888);flex:none}
        .name{font-weight:600;font-size:1.05rem}
        .slabel{color:var(--dim);font-size:.8rem;margin-left:auto;padding-left:6px;text-align:right}
        .about{color:var(--dim);font-size:.85rem;margin:0 0 9px;padding:0 0 9px;border-bottom:1px dashed var(--line);white-space:pre-line}
        .about b{color:var(--text)}
        .body{flex:1 1 auto;margin-top:2px;display:flex;flex-direction:column;gap:8px}
        .row{display:flex;align-items:center;gap:.6rem;flex-wrap:wrap}
        .dot-sm{width:9px;height:9px;border-radius:50%;flex:none}
        .iname{font-weight:600}
        .inote{color:var(--dim);white-space:pre-line}
        .istate{margin-left:auto;font-size:.68rem;font-weight:700;letter-spacing:.06em;text-transform:uppercase}
        .fact{display:flex;gap:9px;align-items:flex-start}
        .tag{flex:none;border:1px solid var(--line);color:var(--accent);font-size:.68rem;font-weight:700;letter-spacing:.4px;text-transform:uppercase;border-radius:5px;padding:2px 8px;margin-top:2px;white-space:nowrap}
        .txt{flex:1;min-width:0;overflow-wrap:anywhere;white-space:pre-line;color:var(--dim)}
        .waiting{margin-top:8px;color:var(--dim);font-size:.85rem}
        .foot{flex:none;margin-top:12px;padding-top:10px;border-top:1px solid var(--line)}
        .lbl{display:block;color:var(--dim);font-size:.72rem;text-transform:uppercase;letter-spacing:.4px;margin-bottom:3px}
        .nxt,.nxt-one{margin:3px 0 0;padding-left:15px;position:relative}
        .nxt::before,.nxt-one::before{content:'→';position:absolute;left:0;color:var(--dim);font-size:.8rem}
        .links{margin-top:9px;display:flex;flex-wrap:wrap;gap:7px}
        .links a{background:var(--bg);border:1px solid var(--line);color:var(--accent);padding:3px 10px;border-radius:999px;font-size:.8rem;text-decoration:none}
        .updated{color:var(--dim);font-size:.75rem;margin-top:8px}
        .updated.stale{color:#eab308;font-weight:600}
        h2{font-size:.95rem;margin:24px 0 10px;color:var(--dim);text-transform:uppercase;letter-spacing:.5px}
        .chips{display:flex;flex-wrap:wrap;gap:8px}
        .chip{background:var(--card);border:1px solid var(--line);border-radius:8px;padding:7px 12px;font-size:.85rem}
        .chip b{color:var(--text)}
        .chips.closed .chip{border-left:4px solid #38bdf8}
        .note{color:var(--dim)}
        footer{margin-top:24px;color:var(--dim);font-size:.78rem}
        """;

    // ---- client-side age & staleness ----

    private const string AgeScript = """
        (function () {
          document.querySelectorAll('.updated[data-upd]').forEach(function (el) {
            var iso = el.getAttribute('data-upd').trim();
            var disp = el.getAttribute('data-disp') || iso;
            var t = new Date(iso);
            if (isNaN(t)) return;
            var diff = Date.now() - t.getTime();
            if (diff < 0) {
              el.classList.add('stale');
              el.textContent = 'updated ' + disp + ' · ⚠ future timestamp';
              return;
            }
            var lim = parseInt(el.getAttribute('data-stale-after') || '48', 10);
            if (!(lim > 0)) lim = 48;
            // compare exact milliseconds: rounding is for display only
            if (diff > lim * 3600000) {
              el.classList.add('stale');
              el.textContent = '⚠ updated ' + disp + ' · ' + Math.round(diff / 3600000) + 'h ago — stale';
              return;
            }
            el.textContent = 'updated ' + disp + ' · ' + Math.round(diff / 3600000) + 'h ago';
          });
        })();
        """;
}
