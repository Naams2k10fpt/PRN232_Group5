using System;
using System.Collections.Generic;

namespace BusinessObjects;

public partial class Subject
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    public string Name { get; set; } = null!;

    public bool IsDeleted { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public virtual ICollection<Deck> Decks { get; set; } = new List<Deck>();

    public virtual User User { get; set; } = null!;
}
