using System;

namespace BusinessObjects;

public partial class GroupMessage
{
    public int Id { get; set; }

    public int GroupId { get; set; }

    public string SenderUserId { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public virtual StudyGroup Group { get; set; } = null!;

    public virtual User SenderUser { get; set; } = null!;
}
