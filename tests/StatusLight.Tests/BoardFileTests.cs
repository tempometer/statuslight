using StatusLight;

namespace StatusLight.Tests;

public sealed class BoardFileTests : IDisposable
{
    private readonly string _dir =
        Path.Combine(Path.GetTempPath(), "statuslight-tests-" + Guid.NewGuid().ToString("N"));

    private string WriteYaml(string content)
    {
        Directory.CreateDirectory(_dir);
        var path = Path.Combine(_dir, "status.yml");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void Load_ValidFile_MapsTitleAndItems()
    {
        var path = WriteYaml("""
            title: Deploy health
            items:
              - name: API
                state: green
                note: v2.4.1 live
              - name: Worker
                state: yellow
                note: queue lag 4m
              - name: Billing sync
                state: red
            """);

        var board = BoardFile.Load(path);

        Assert.Equal("Deploy health", board.Title);
        Assert.True(board.IsLegacyList);
        var items = board.Sections[0].Items;
        Assert.Equal(3, items.Count);
        Assert.Equal(new BoardItem("API", States.Green, "v2.4.1 live"), items[0]);
        Assert.Equal(new BoardItem("Worker", States.Yellow, "queue lag 4m"), items[1]);
        Assert.Equal(new BoardItem("Billing sync", States.Red), items[2]);
        Assert.Null(items[2].Note);
    }

    [Fact]
    public void Load_StateIsCaseInsensitive()
    {
        var path = WriteYaml("""
            title: T
            items:
              - name: A
                state: GREEN
            """);

        var board = BoardFile.Load(path);

        Assert.Equal(States.Green, board.Sections[0].Items[0].State);
    }

    [Fact]
    public void Load_NoteIsTrimmedAndBlankBecomesNull()
    {
        var path = WriteYaml("""
            title: T
            items:
              - name: A
                state: green
                note: "  spaced  "
              - name: B
                state: green
                note: "   "
            """);

        var board = BoardFile.Load(path);

        var items = board.Sections[0].Items;
        Assert.Equal("spaced", items[0].Note);
        Assert.Null(items[1].Note);
    }

    [Fact]
    public void Load_UnknownState_RejectedWithItemIndex()
    {
        var path = WriteYaml("""
            title: T
            items:
              - name: A
                state: green
              - name: B
                state: purple
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("items[1].state", ex.Message);
        Assert.Contains("purple", ex.Message);
    }

    [Fact]
    public void Load_MissingTitle_Rejected()
    {
        var path = WriteYaml("""
            items:
              - name: A
                state: green
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("title", ex.Message);
    }

    [Fact]
    public void Load_EmptyTitle_Rejected()
    {
        var path = WriteYaml("""
            title: ""
            items:
              - name: A
                state: green
            """);

        Assert.Throws<BoardFileException>(() => BoardFile.Load(path));
    }

    [Fact]
    public void Load_MissingItems_Rejected()
    {
        var path = WriteYaml("title: T");

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("items", ex.Message);
    }

    [Fact]
    public void Load_EmptyItemsList_Rejected()
    {
        var path = WriteYaml("""
            title: T
            items: []
            """);

        Assert.Throws<BoardFileException>(() => BoardFile.Load(path));
    }

    [Fact]
    public void Load_ItemWithoutName_Rejected()
    {
        var path = WriteYaml("""
            title: T
            items:
              - state: green
            """);

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("items[0].name", ex.Message);
    }

    [Fact]
    public void Load_MissingFile_RejectedWithPath()
    {
        var path = Path.Combine(_dir, "does-not-exist.yml");

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Equal(path, ex.SourcePath);
        Assert.Contains("cannot read file", ex.Message);
    }

    [Fact]
    public void Load_MalformedYaml_RejectedNotLeaked()
    {
        var path = WriteYaml("title: [unclosed");

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("invalid content", ex.Message);
    }

    [Fact]
    public void Load_EmptyDocument_Rejected()
    {
        var path = WriteYaml("");

        var ex = Assert.Throws<BoardFileException>(() => BoardFile.Load(path));

        Assert.Contains("empty", ex.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }
}
