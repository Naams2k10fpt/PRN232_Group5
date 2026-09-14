using System;
using System.Collections.Generic;

namespace BusinessObjects;

public partial class Deck
{
    public int Id { get; set; }

    public int SubjectId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public int TimeLimitMinutes { get; set; }

    public virtual ICollection<Question> Questions { get; set; } = new List<Question>();

    public virtual Subject Subject { get; set; } = null!;

    public virtual ICollection<TestHistory> TestHistories { get; set; } = new List<TestHistory>();
}
