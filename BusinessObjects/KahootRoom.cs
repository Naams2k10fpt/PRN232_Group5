using System;
using System.Collections.Generic;

namespace BusinessObjects;

public partial class KahootRoom
{
    public int Id { get; set; }

    public string RoomCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string HostUserId { get; set; } = null!;

    public int DeckId { get; set; }

    public string Status { get; set; } = "Waiting"; // Waiting, InProgress, Finished

    public int CurrentQuestionIndex { get; set; }

    public int TotalQuestions { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual User HostUser { get; set; } = null!;

    public virtual Deck Deck { get; set; } = null!;

    public virtual ICollection<KahootParticipant> Participants { get; set; } = new List<KahootParticipant>();
}
