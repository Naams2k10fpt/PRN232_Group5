using System;
using System.Collections.Generic;

namespace BusinessObjects;

public partial class KahootParticipant
{
    public int Id { get; set; }

    public int RoomId { get; set; }

    public string? UserId { get; set; }

    public string Nickname { get; set; } = null!;

    public string? ConnectionId { get; set; }

    public int TotalScore { get; set; }

    public int CurrentRank { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public virtual KahootRoom Room { get; set; } = null!;

    public virtual User? User { get; set; }

    public virtual ICollection<KahootAnswer> Answers { get; set; } = new List<KahootAnswer>();
}
