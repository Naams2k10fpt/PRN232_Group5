using System;
using System.Collections.Generic;

namespace BusinessObjects;

public partial class User
{
    public string Id { get; set; } = null!;

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? PasswordHash { get; set; }

    public string? Role { get; set; }

    public string? AvatarUrl { get; set; }

    public bool IsDisabled { get; set; }

    public bool EmailConfirmed { get; set; }

    public string? SecurityStamp { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Subject> Subjects { get; set; } = new List<Subject>();

    public virtual ICollection<TestHistory> TestHistories { get; set; } = new List<TestHistory>();

    public virtual ICollection<FlashcardProgress> FlashcardProgresses { get; set; } = new List<FlashcardProgress>();
}
