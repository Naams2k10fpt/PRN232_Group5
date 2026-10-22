namespace BusinessObjects;

public sealed class ChatChannel
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string Name { get; set; } = "chung";
    public string? Topic { get; set; }
    public bool IsDefault { get; set; }
    public bool IsArchived { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public StudyGroup Group { get; set; } = null!;
    public ICollection<GroupMessage> Messages { get; set; } = new List<GroupMessage>();
    public ICollection<ChatChannelRead> Reads { get; set; } = new List<ChatChannelRead>();
}

public sealed class ChatChannelRead
{
    public int ChannelId { get; set; }
    public string UserId { get; set; } = "";
    public int LastReadMessageId { get; set; }
    public ChatChannel Channel { get; set; } = null!;
    public User User { get; set; } = null!;
}
