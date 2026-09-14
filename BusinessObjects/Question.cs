using System;
using System.Collections.Generic;

namespace BusinessObjects;

public partial class Question
{
    public int Id { get; set; }

    public int DeckId { get; set; }

    public string Content { get; set; } = null!;

    public string? Explanation { get; set; }

    public int QuestionType { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();

    public virtual Deck Deck { get; set; } = null!;

    public virtual ICollection<TestResultDetail> TestResultDetails { get; set; } = new List<TestResultDetail>();

    public virtual ICollection<FlashcardProgress> FlashcardProgresses { get; set; } = new List<FlashcardProgress>();
}
