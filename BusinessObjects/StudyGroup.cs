using System;
using System.Collections.Generic;

namespace BusinessObjects;

// A study group with a realtime chat (ChatHub). Members join; messages are kept for paged history.
public partial class StudyGroup
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int? SubjectId { get; set; }

    public string CreatedByUserId { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Subject? Subject { get; set; }

    public virtual User CreatedByUser { get; set; } = null!;

    public virtual ICollection<StudyGroupMember> Members { get; set; } = new List<StudyGroupMember>();

    public virtual ICollection<GroupMessage> Messages { get; set; } = new List<GroupMessage>();
}
