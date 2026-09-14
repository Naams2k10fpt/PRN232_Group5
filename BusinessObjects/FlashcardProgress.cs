namespace BusinessObjects;

public class FlashcardProgress
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public int QuestionId { get; set; }
    public int Repetition { get; set; }
    public int IntervalMinutes { get; set; } = 10;
    public double EaseFactor { get; set; } = 2.5;
    public DateTime LastReviewedAtUtc { get; set; }
    public DateTime NextReviewAtUtc { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual Question Question { get; set; } = null!;

    public void Review(bool remembered, DateTime reviewedAtUtc)
    {
        // ponytail: binary schedule matches the current Again/Known UI;
        // add four-grade SM-2 only when the product exposes those grades.
        if (!remembered)
        {
            Repetition = 0;
            IntervalMinutes = 10;
            EaseFactor = Math.Max(1.3, EaseFactor - 0.2);
        }
        else
        {
            Repetition++;
            IntervalMinutes = Repetition switch
            {
                1 => 24 * 60,
                2 => 3 * 24 * 60,
                _ => Math.Max(24 * 60, (int)Math.Round(IntervalMinutes * EaseFactor))
            };
            EaseFactor = Math.Min(3, EaseFactor + 0.1);
        }

        LastReviewedAtUtc = reviewedAtUtc;
        NextReviewAtUtc = reviewedAtUtc.AddMinutes(IntervalMinutes);
    }
}
