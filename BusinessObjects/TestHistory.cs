using System;
using System.Collections.Generic;

namespace BusinessObjects;

public partial class TestHistory
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public int DeckId { get; set; }

    public Guid? QuizAttemptId { get; set; }

    public string? ResultSnapshotJson { get; set; }

    public double Score { get; set; }

    public double Percentage { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Deck Deck { get; set; } = null!;

    public virtual QuizAttempt? QuizAttempt { get; set; }

    public virtual ICollection<TestResultDetail> TestResultDetails { get; set; } = new List<TestResultDetail>();

    public virtual User User { get; set; } = null!;
}
