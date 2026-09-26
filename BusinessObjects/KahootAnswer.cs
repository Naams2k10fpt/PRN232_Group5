using System;

namespace BusinessObjects;

public partial class KahootAnswer
{
    public int Id { get; set; }

    public int ParticipantId { get; set; }

    public int QuestionId { get; set; }

    public int? SelectedAnswerId { get; set; }

    public bool IsCorrect { get; set; }

    public decimal AnswerTimeSeconds { get; set; }

    public int ScoreEarned { get; set; }

    public virtual KahootParticipant Participant { get; set; } = null!;

    public virtual Question Question { get; set; } = null!;

    public virtual Answer? SelectedAnswer { get; set; }
}
