namespace BusinessObjects;

public sealed class StudyGroupMember
{
    public int GroupId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public int? LastReadMessageId { get; set; }
    public StudyGroup Group { get; set; } = null!;
    public User User { get; set; } = null!;
}
