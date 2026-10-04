namespace BusinessObjects;

public enum GroupMessageKind { Chat, Joined, Left }

public sealed class GroupMessage
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public GroupMessageKind Kind { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public StudyGroup Group { get; set; } = null!;
    public User SenderUser { get; set; } = null!;
}
