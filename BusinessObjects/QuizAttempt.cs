namespace BusinessObjects;

public class QuizAttempt
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = null!;

    public int DeckId { get; set; }

    public string QuestionIdsJson { get; set; } = null!;

    public int TimeLimitMinutes { get; set; }

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Deck Deck { get; set; } = null!;

    public virtual TestHistory? TestHistory { get; set; }
}
