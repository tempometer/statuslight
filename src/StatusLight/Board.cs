namespace StatusLight;

public enum BoardState
{
    Green,
    Yellow,
    Red
}

public sealed record BoardItem(string Name, BoardState State, string? Note = null);

public sealed record Board(string Title, IReadOnlyList<BoardItem> Items);
