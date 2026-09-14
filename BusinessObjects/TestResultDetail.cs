using System;
using System.Collections.Generic;

namespace BusinessObjects;

public partial class TestResultDetail
{
    public int Id { get; set; }

    public int TestHistoryId { get; set; }

    public int QuestionId { get; set; }

    public int? SelectedAnswerId { get; set; }

    public bool IsCorrect { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual Answer? SelectedAnswer { get; set; }

    public virtual TestHistory TestHistory { get; set; } = null!;
}
