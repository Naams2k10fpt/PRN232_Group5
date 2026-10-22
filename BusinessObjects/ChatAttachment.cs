namespace BusinessObjects;

// Small study documents are stored with their message in PostgreSQL, so deployments do not lose files.
public sealed class ChatAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int MessageId { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public int Size { get; set; }
    public byte[] Data { get; set; } = [];
    public GroupMessage Message { get; set; } = null!;
}

public sealed class ChatReaction
{
    public int MessageId { get; set; }
    public string UserId { get; set; } = "";
    public string Emoji { get; set; } = "";
    public GroupMessage Message { get; set; } = null!;
    public User User { get; set; } = null!;
}
