namespace BusinessObjects;

public class QuestionReport
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string UserId { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string? Note { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual Question Question { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
