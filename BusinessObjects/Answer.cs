using System;
using System.Collections.Generic;

namespace BusinessObjects;

public partial class Answer
{
    public int Id { get; set; }

    public int QuestionId { get; set; }

    public string Content { get; set; } = null!;

    public bool IsCorrect { get; set; }

    public virtual Question Question { get; set; } = null!;

    public virtual ICollection<TestResultDetail> TestResultDetails { get; set; } = new List<TestResultDetail>();
}
