namespace BusinessObjects;

public enum FavoriteKind { Subject, Deck }

public sealed class SubjectFavorite
{
    public string UserId { get; set; } = null!;
    public int SubjectId { get; set; }
    public Subject Subject { get; set; } = null!;
    public bool IsFavorite { get; set; }
}

public sealed class DeckFavorite
{
    public string UserId { get; set; } = null!;
    public int DeckId { get; set; }
    public Deck Deck { get; set; } = null!;
    public bool IsFavorite { get; set; }
}
