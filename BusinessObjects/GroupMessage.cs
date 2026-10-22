namespace BusinessObjects;

public enum GroupMessageKind { Chat, Joined, Left }

public sealed class GroupMessage
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public int ChannelId { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public GroupMessageKind Kind { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? EditedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int Revision { get; set; }
    public int? ReplyToMessageId { get; set; }
    public Guid? ClientMessageId { get; set; }
    public ChatChannel Channel { get; set; } = null!;
    public GroupMessage? ReplyToMessage { get; set; }
    public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
    public ICollection<ChatReaction> Reactions { get; set; } = new List<ChatReaction>();
    public StudyGroup Group { get; set; } = null!;
    public User SenderUser { get; set; } = null!;
}
