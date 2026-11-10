namespace BusinessObjects;

public class FlashcardReviewReceipt
{
    public int ProgressId { get; set; }
    public Guid ReviewId { get; set; }
    public FlashcardProgress Progress { get; set; } = null!;
}
